#!/usr/bin/env python3
"""Explicit local G03 qualification; never invoke arbitrary platform actions.

Requires the dedicated TDSBLive test trigger installed in Streamer.bot.
Only an isolated temporary host and synthetic probe are used. Pause/Resume
operate the real Speaker.bot queue and require the explicit command flag.
"""
import argparse
import http.cookiejar
import json
from pathlib import Path
import subprocess
import tempfile
import time
import urllib.request
from uuid import uuid4
from qualify_foundation import unused_port, wait_ready
from rumble_evidence import ROOT, scan_bytes


def qualify(speaker_port, refresh_contract):
    with tempfile.TemporaryDirectory(prefix='tdsblive-g03-') as directory:
        port = unused_port()
        configuration = {'server': {'host': '127.0.0.1', 'port': port},
            'streamerBot': {'host': '127.0.0.1', 'port': 8080, 'enabled': True, 'forwardLiveEvents': True,
                'allowedActionIds': ['5720ba36-c98a-5b34-94ba-c08a2ff16300']},
            'speakerBot': {'host': '127.0.0.1', 'port': speaker_port, 'enabled': True}}
        Path(directory, 'configuration.json').write_text(json.dumps(configuration))
        process = subprocess.Popen(['dotnet', str(ROOT / 'src/ExtensionSuite.Host/bin/Release/net10.0/ExtensionSuite.Host.dll'),
            '--TDSBLive:DataDirectory', directory], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        origin = f'http://127.0.0.1:{port}'
        client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        def get(path):
            with client.open(origin + path, timeout=5) as response:
                payload = response.read()
                # Authentication responses are consumed in memory only, never written to a file or output.
                if path == '/api/auth/csrf': return json.loads(payload)
                return json.loads(scan_bytes(payload, 'G03 runtime response'))
        def post(path, body):
            csrf = get('/api/auth/csrf')['requestToken']
            request = urllib.request.Request(origin + path, method='POST', data=json.dumps(body).encode(),
                headers={'Content-Type': 'application/json', 'X-TDSBLive-CSRF': csrf})
            with client.open(request, timeout=15) as response:
                return json.loads(scan_bytes(response.read(), 'G03 runtime response'))
        try:
            wait_ready(process, origin)
            deadline = time.monotonic() + 30
            while True:
                state = get('/api/integrations')
                if all(state[name]['state'] == 'connected' for name in ['streamerBot', 'speakerBot']): break
                if time.monotonic() > deadline: raise RuntimeError('Bot connection qualification timed out')
                time.sleep(.2)
            discovery = get('/api/integrations/streamerbot/discovery')
            assert any(t['eventName'] == 'tdsblive.test.trigger' for t in discovery['codeTriggers'])
            nonce = str(uuid4())
            result = post('/api/integrations/streamerbot/triggers/execute', {'eventName': 'tdsblive.test.trigger', 'arguments': {'tdsbliveTestId': nonce}, 'executeLive': True})
            assert result['state'] == 'acknowledged'
            deadline = time.monotonic() + 10
            while True:
                matches = [e for e in get('/api/inspector') if e.get('event') and e['event'].get('nativeId') == nonce]
                if matches: break
                if time.monotonic() > deadline: raise RuntimeError('Real trigger execution was not observed')
                time.sleep(.1)
            entry = matches[0]; assert entry['event']['provenance'] == 'simulation'
            replay = post('/api/inspector/' + entry['id'] + '/replay', {'persist': False})
            assert not replay['persisted'] and not replay['liveActionsAllowed']
            fixture = get('/api/inspector/' + entry['id'] + '/fixture')
            assert fixture['event']['nativeId'] == nonce
            forward_nonce = str(uuid4())
            forwarded = post('/api/integrations/streamerbot/actions/execute', {'actionId': '5720ba36-c98a-5b34-94ba-c08a2ff16300',
                'arguments': {'tdsbliveForwardedSource': 'Twitch', 'tdsbliveForwardedType': 'ChatMessage', 'messageId': forward_nonce,
                    'message': 'Synthetic forwarder qualification', 'userName': 'TDSBLive test', 'isTest': True}, 'executeLive': True})
            assert forwarded['state'] == 'acknowledged'
            deadline = time.monotonic() + 10
            while True:
                messages = [e for e in get('/api/inspector') if e.get('event') and e['event'].get('nativeId') == forward_nonce]
                if messages: break
                if time.monotonic() > deadline: raise RuntimeError('Forwarding template execution was not observed')
                time.sleep(.1)
            assert messages[0]['event']['type'] == 'chat.message' and messages[0]['event']['provenance'] == 'simulation'
            assert post('/api/integrations/speakerbot/queue', {'operation': 'Pause', 'executeLive': True})['state'] == 'acknowledged'
            try:
                assert post('/api/integrations/speakerbot/speak', {'voice': 'local english', 'message': 'TDSBLive synthetic qualification', 'executeLive': False})['state'] == 'simulated'
            finally:
                assert post('/api/integrations/speakerbot/queue', {'operation': 'Resume', 'executeLive': True})['state'] == 'acknowledged'
            if refresh_contract:
                contract = get('/api/openapi/v1.json'); contract.pop('servers', None)
                (ROOT / 'docs/contracts/openapi.json').write_text(json.dumps(contract, indent=2) + '\n')
            print('PASS: real custom-trigger execution, inspector sample, isolated replay, Speaker.bot Pause/Resume.')
            print('Versions:', state['streamerBot'].get('version'), state['speakerBot'].get('version'))
        finally:
            process.terminate()
            try: process.wait(timeout=10)
            except subprocess.TimeoutExpired: process.kill(); process.wait(timeout=10)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--execute-local-test-trigger-and-queue', action='store_true', required=True)
    parser.add_argument('--speaker-port', type=int, default=7680); parser.add_argument('--refresh-contract', action='store_true')
    args = parser.parse_args(); qualify(args.speaker_port, args.refresh_contract)
