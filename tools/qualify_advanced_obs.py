#!/usr/bin/env python3
"""Serve an isolated G11 OBS example. Enter stops it and removes its temporary data."""
import argparse
from datetime import datetime, timezone
from pathlib import Path
import json
import subprocess
import tempfile
import uuid

from qualify_financial import Client, insert_events, owned_event, wait_projected
from qualify_foundation import wait_ready
from rumble_evidence import ROOT


def run(port):
    with tempfile.TemporaryDirectory(prefix='tdsblive-g11-obs-') as directory:
        root = Path(directory)
        root.joinpath('configuration.json').write_text(json.dumps({'server': {'host': '127.0.0.1', 'port': port}}))
        origin = f'http://127.0.0.1:{port}'
        process = subprocess.Popen(['dotnet', str(ROOT / 'src/ExtensionSuite.Host/bin/Release/net10.0/ExtensionSuite.Host.dll'),
            '--TDSBLive:DataDirectory', directory, '--TDSBLive:OpenEditor=false'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        try:
            wait_ready(process, origin)
            client = Client(origin)
            event = owned_event('g11-owned-support', 'kofi', {'kind': 'donation', 'quantity': 1,
                'nativeMoney': {'amountMinor': 1250, 'currency': 'USD', 'minorUnitDigits': 2}})
            event['occurredAt'] = datetime.now(timezone.utc).isoformat()
            event['user'] = {'platformUserId': 'g11-owned', 'displayName': 'Owned supporter (test)'}
            insert_events(root / 'tdsblive.db', [event]); wait_projected(client, 1)
            video_path = ROOT / 'tests/fixtures/media/synthetic-pattern.webm'
            subprocess.run(['sonar', 'analyze', 'secrets', str(video_path)], check=True, stdout=subprocess.DEVNULL)
            import urllib.request
            request = urllib.request.Request(origin + '/api/assets', method='POST', data=video_path.read_bytes(),
                headers={'Content-Type': 'video/webm', 'X-Asset-Filename': 'g11-owned.webm', 'X-TDSBLive-CSRF': client.nonce})
            video = json.load(client.opener.open(request))
            group = str(uuid.uuid4())
            widgets = [
                {'kind': 'text', 'name': 'Owned qualification title', 'text': 'G11 owned OBS qualification', 'x': 20, 'y': 10, 'width': 950, 'height': 60},
                {'kind': 'event-list', 'name': 'Activity', 'x': 20, 'y': 90, 'width': 950, 'height': 100, 'eventList': {'template': '{user} · {type}'}},
                {'kind': 'goal-bar', 'name': 'Ledger goal', 'groupId': group, 'x': 20, 'y': 220, 'width': 950, 'height': 120,
                    'progress': {'source': 'ledger-usd', 'target': 100, 'label': 'Owned USD goal'}},
                {'kind': 'progress-bar', 'name': 'Manual progress', 'groupId': group, 'x': 20, 'y': 360, 'width': 950, 'height': 120,
                    'progress': {'value': 25, 'target': 50, 'label': 'Manual progress'}},
                {'kind': 'latest-supporter', 'name': 'Latest owned supporter', 'x': 20, 'y': 500, 'width': 950, 'height': 100},
                {'kind': 'video', 'name': 'Owned video', 'x': 20, 'y': 640, 'width': 320, 'height': 240, 'assetId': video['id']},
            ]
            for widget in widgets:
                widget['id'] = str(uuid.uuid4())
            client.call('/api/overlays', {'id': 'g11-obs', 'name': 'Owned G11 OBS example', 'width': 1000, 'height': 1000,
                'canvasEnabled': True, 'widgets': widgets}, 'POST')
            print(f'Owned OBS URL: {origin}/overlay/g11-obs (1000 x 1000)', flush=True)
            print('Integrations disabled; one owned support example; video silent. Enter stops and removes the host.', flush=True)
            input()
        finally:
            if process.poll() is None:
                process.terminate(); process.wait(timeout=15)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--port', type=int, default=17475)
    args = parser.parse_args()
    if not 1024 <= args.port <= 65535:
        parser.error('Port must be 1024–65535')
    try:
        run(args.port)
    except KeyboardInterrupt:
        print('Owned G11 host stopped and temporary data removed.')
