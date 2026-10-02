#!/usr/bin/env python3
"""Explicit installed Streamer.bot Ko-fi forwarding check with a synthetic test event.

Uses a disposable host, one previously imported TDSBLive forwarding action,
and no payment, Speaker.bot queue operation or production financial write.
"""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile
import time
from uuid import uuid4
from qualify_financial import Client
from qualify_foundation import unused_port, wait_ready
from rumble_evidence import ROOT

ACTION = '5720ba36-c98a-5b34-94ba-c08a2ff16300'

def qualify():
    with tempfile.TemporaryDirectory(prefix='tdsblive-g07-kofi-') as directory:
        port = unused_port()
        configuration = {'server': {'host': '127.0.0.1', 'port': port},
            'streamerBot': {'host': '127.0.0.1', 'port': 8080, 'enabled': True,
                'forwardLiveEvents': False, 'allowedActionIds': [ACTION]}}
        Path(directory, 'configuration.json').write_text(json.dumps(configuration))
        process = subprocess.Popen(['dotnet', str(ROOT / 'src/ExtensionSuite.Host/bin/Release/net10.0/ExtensionSuite.Host.dll'),
            '--TDSBLive:DataDirectory', directory], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        origin = f'http://127.0.0.1:{port}'
        try:
            wait_ready(process, origin)
            client = Client(origin)
            deadline = time.monotonic() + 30
            while client.call('/api/integrations')['streamerBot']['state'] != 'connected':
                if time.monotonic() > deadline: raise RuntimeError('Installed Streamer.bot connection timed out')
                time.sleep(.1)
            native_id = str(uuid4())
            result = client.call('/api/integrations/streamerbot/actions/execute', {'actionId': ACTION,
                'arguments': {'tdsbliveForwardedSource': 'Kofi', 'tdsbliveForwardedType': 'Donation',
                    'messageId': native_id, 'amount': '12.50', 'currency': 'USD', 'from': 'Owned fixture',
                    'timestamp': '2026-01-05T12:00:00Z', 'isTest': True}, 'executeLive': True}, 'POST')
            assert result['state'] == 'acknowledged'
            deadline = time.monotonic() + 15
            while True:
                matches = [row['event'] for row in client.call('/api/inspector') if row.get('event') and row['event'].get('nativeId') == native_id]
                if matches: break
                if time.monotonic() > deadline: raise RuntimeError('Synthetic Ko-fi forwarding was not observed')
                time.sleep(.1)
            event = matches[0]
            assert event['platform'] == 'kofi' and event['type'] == 'support.donation'
            assert event['provenance'] == 'simulation'
            assert event['support']['nativeMoney'] == {'amountMinor': 1250, 'currency': 'USD', 'minorUnitDigits': 2}
            assert not event['support'].get('gatedReason')
            assert all(row['nativeEventId'] != native_id for row in client.call('/api/financial/ledger')['items'])
            print('PASS: installed CPH forwarder → General.Custom → typed Ko-fi USD support; simulation excluded from ledger')
        finally:
            if process.poll() is None:
                process.terminate()
                try: process.wait(timeout=10)
                except subprocess.TimeoutExpired: process.kill(); process.wait(timeout=10)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--execute-synthetic-forwarding-action', action='store_true', required=True)
    parser.parse_args()
    qualify()
