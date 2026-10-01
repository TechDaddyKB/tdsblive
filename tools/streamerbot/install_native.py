#!/usr/bin/env python3
"""Import the TDSBLive bundle into a stopped Streamer.bot 1.0.7 action store.

Operator-only CLI, never a web endpoint. Preserve unrelated actions and bindings.
Requires the deterministic scanner; refuses files containing credentials.
"""
import argparse
import base64
from datetime import datetime, timezone
import gzip
import io
import json
import os
from pathlib import Path
import socket
import subprocess
import sys
from uuid import UUID, uuid5

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
from rumble_evidence import safe_read, scan_bytes

NAMESPACE = UUID('90f0339e-036b-446b-a6fa-07abc3c783d8')
OWN_ACTIONS = {str(uuid5(NAMESPACE, name)) for name in ['TDSBLive bootstrap', 'TDSBLive qualification probe', 'TDSBLive explicit forwarder']}
SOURCES = {'TDSBLive bootstrap': 'TDSBLiveBootstrap.cs', 'TDSBLive qualification probe': 'TDSBLiveQualificationProbe.cs',
           'TDSBLive explicit forwarder': 'TDSBLiveForwardEvent.cs'}


def load_bundle(path):
    if path.stat().st_size > 1048576: raise ValueError('Import bundle exceeds 1 MiB')
    encoded = base64.b64decode(safe_read(path), validate=True)
    if encoded[:4] != b'SBAE': raise ValueError('Unsupported import envelope')
    with gzip.GzipFile(fileobj=io.BytesIO(encoded[4:])) as compressed:
        decoded = compressed.read(2097153)
    if len(decoded) > 2097152: raise ValueError('Decoded import exceeds 2 MiB')
    document = json.loads(scan_bytes(decoded, 'decoded TDSBLive import'))
    if document.get('meta', {}).get('name') != 'TDSBLive G03 integrations': raise ValueError('Only the TDSBLive bundle is supported')
    imported = document.get('data', {}).get('actions', [])
    if len(imported) != 3 or {a.get('id') for a in imported} != OWN_ACTIONS: raise ValueError('Unexpected bundle action identifiers')
    for action in imported:
        if action.get('group') != 'TDSBLive' or len(action.get('actions', [])) != 1: raise ValueError('Unexpected action definition')
        inline = action['actions'][0]
        if inline.get('type') != 99999: raise ValueError('Only the reviewed C# bootstrap templates are supported')
        source = scan_bytes(base64.b64decode(inline['byteCode'], validate=True), 'decoded bootstrap source')
        if action.get('name') not in SOURCES or source != safe_read(ROOT / 'integrations/streamerbot' / SOURCES[action['name']]):
            raise ValueError('Bundle source does not match the checked-out TDSBLive templates')
    queue = document.get('data', {}).get('queues', [])
    if queue != [{'id': str(uuid5(NAMESPACE, 'queue')), 'name': 'TDSBLive', 'blocking': False}]: raise ValueError('Unexpected bundle queue')
    return document


def install(bundle, data_directory, port, bind_probe):
    if not 1 <= port <= 65535: raise ValueError('Port must be 1..65535')
    with socket.socket() as check:
        check.settimeout(.5)
        if check.connect_ex(('127.0.0.1', port)) == 0: raise RuntimeError('Stop Streamer.bot before importing the native action store')
    directory = data_directory.resolve(strict=True)
    target = directory / 'actions.json'
    if target.is_symlink() or not target.is_file(): raise ValueError('An existing regular data/actions.json is required')
    scanned = subprocess.run(['sonar', 'analyze', 'secrets', str(target)], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False)
    if scanned.returncode != 0: raise RuntimeError('STOP: native action store failed deterministic secrets scanning; no content was read')
    original = target.read_bytes()
    native = json.loads(original)
    if not isinstance(native.get('actions'), list) or not isinstance(native.get('queues'), list): raise ValueError('Unsupported native action-store schema')
    imported = load_bundle(bundle)
    existing = {a['id']: a for a in native['actions']}
    if len(existing) != len(native['actions']): raise ValueError('Native store contains duplicate action identifiers')
    for exported in imported['data']['actions']:
        action = dict(exported); action['subActions'] = action.pop('actions')
        previous = existing.get(action['id'])
        if previous is not None:
            if previous.get('group') != 'TDSBLive': raise ValueError('Refusing to overwrite an unrelated action')
            action['triggers'] = previous.get('triggers', [])
        if bind_probe and action['name'] == 'TDSBLive qualification probe':
            trigger_id = str(uuid5(NAMESPACE, 'qualification-trigger'))
            action['triggers'] = [t for t in action['triggers'] if t.get('type') != 18002 or t.get('eventName') != 'tdsblive.test.trigger']
            action['triggers'].append({'id': trigger_id, 'type': 18002, 'enabled': True, 'eventName': 'tdsblive.test.trigger'})
        existing[action['id']] = action
    native['actions'] = list(existing.values())
    queue_ids = {q['id'] for q in native['queues']}
    native['queues'].extend(q for q in imported['data']['queues'] if q['id'] not in queue_ids)
    backup = directory / ('actions.tdsblive-backup-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ') + '.json')
    with os.fdopen(os.open(backup, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), 'wb') as stream: stream.write(original)
    temporary = directory / 'actions.tdsblive-import.tmp'
    try:
        with temporary.open('x', encoding='utf-8-sig') as stream:
            json.dump(native, stream, ensure_ascii=False, indent=2)
            stream.flush(); os.fsync(stream.fileno())
        os.replace(temporary, target)
    finally:
        temporary.unlink(missing_ok=True)
    print('Imported three TDSBLive actions; preserved unrelated actions and created a private backup.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('bundle', type=Path)
    parser.add_argument('--data-directory', type=Path, required=True)
    parser.add_argument('--streamer-port', type=int, default=8080)
    parser.add_argument('--bind-qualification-probe', action='store_true')
    parser.add_argument('--native-version', choices=['1.0.7'], required=True)
    args = parser.parse_args()
    install(args.bundle, args.data_directory, args.streamer_port, args.bind_qualification_probe)
