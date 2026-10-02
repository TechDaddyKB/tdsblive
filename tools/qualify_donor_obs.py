#!/usr/bin/env python3
"""Run an owned, disposable donor overlay for manual OBS verification; Ctrl+C stops and removes it."""
import argparse
from datetime import datetime, timedelta, timezone
import json
from pathlib import Path
import subprocess
import tempfile
import time
import uuid

from qualify_financial import Client, insert_events, owned_event, wait_projected
from qualify_foundation import wait_ready
from rumble_evidence import ROOT


def run(port):
    dll = ROOT / 'src/ExtensionSuite.Host/bin/Release/net10.0/ExtensionSuite.Host.dll'
    if not dll.is_file():
        raise RuntimeError('Build the Release host before OBS qualification')
    with tempfile.TemporaryDirectory(prefix='tdsblive-g08-obs-') as directory:
        root = Path(directory)
        root.joinpath('configuration.json').write_text(json.dumps({'server': {'host': '127.0.0.1', 'port': port},
            'streamerBot': {'host': '127.0.0.1', 'port': 8080, 'enabled': False},
            'speakerBot': {'host': '127.0.0.1', 'port': 7680, 'enabled': False}, 'rumble': {'enabled': False}}))
        origin = f'http://127.0.0.1:{port}'
        process = subprocess.Popen(['dotnet', str(dll), '--TDSBLive:DataDirectory', directory], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        try:
            wait_ready(process, origin)
            client = Client(origin)
            now = datetime.now(timezone.utc)
            client.call('/api/financial/settings', {'timeZone': 'UTC', 'currentStreamStartUtc': (now - timedelta(minutes=1)).isoformat(), 'version': 0}, 'PUT')
            receipts = 0

            def contribute(name, platform, amount):
                nonlocal receipts
                event = owned_event('obs-' + uuid.uuid4().hex, platform, {'kind': 'donation', 'quantity': 1,
                    'nativeMoney': {'amountMinor': amount, 'currency': 'USD', 'minorUnitDigits': 2}})
                event['occurredAt'] = datetime.now(timezone.utc).isoformat()
                event['user'] = {'platformUserId': 'owned-' + name.lower(), 'displayName': name + ' (test)'}
                insert_events(root / 'tdsblive.db', [event]); receipts += 1
                wait_projected(client, receipts)

            contribute('Alice', 'kofi', 1250); contribute('Bob', 'twitch', 1000)
            kinds = ['donor-crown', 'donor-leaderboard', 'latest-supporter', 'current-stream-leader', 'current-stream-total']
            client.call('/api/overlays', {'id': 'donor-obs-check', 'name': 'G08 owned OBS qualification', 'width': 1000, 'height': 1000,
                'canvasEnabled': True, 'widgets': [{'id': str(uuid.uuid4()), 'kind': kind, 'name': kind, 'x': 20, 'y': index * 190 + 20,
                    'width': 950, 'height': 180, 'fontSize': 32, 'donor': {'template': kind + ': {name} {amount}', 'transitionMs': 500}}
                    for index, kind in enumerate(kinds)]}, 'POST')
            print(f'OBS Browser Source: {origin}/overlay/donor-obs-check', flush=True)
            print('Size: 1000 x 1000. Owned temporary data; integrations disabled; pre-acknowledged outbox.', flush=True)
            print('Alice/Bob leadership alternates every 20 seconds; Ctrl+C stops this host and removes its data.', flush=True)
            for index in range(100):
                time.sleep(20)
                if process.poll() is not None:
                    raise RuntimeError('Qualification host stopped')
                name, platform = ('Bob', 'twitch') if index % 2 == 0 else ('Alice', 'kofi')
                contribute(name, platform, 2000)
                print(f'Owned {name} contribution committed; verify the open OBS overlay updates.', flush=True)
            process.wait()
        finally:
            if process.poll() is None:
                process.terminate(); process.wait(timeout=10)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--port', type=int, default=17475)
    args = parser.parse_args()
    if not 1024 <= args.port <= 65535:
        parser.error('Port must be 1024–65535')
    try:
        run(args.port)
    except KeyboardInterrupt:
        print('Owned OBS qualification stopped and temporary data removed.')
