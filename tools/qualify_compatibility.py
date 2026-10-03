"""Owned G13 utility/OBS host; no live integrations or production data."""
import argparse
import io
import json
import math
from pathlib import Path
import struct
import sqlite3
import subprocess
import tempfile
import time
import urllib.request
import uuid
import wave
import zlib

from qualify_financial import Client, insert_events
from developer import sample_event
from qualify_foundation import unused_port, wait_ready
from rumble_evidence import ROOT, safe_read, scan_bytes

STORE_KEY = 'se:owned'

def owned_png():
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', 1, 1, 8, 6, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(b'\x00\x00\xc8\xff\xff')) + chunk(b'IEND', b'')


def ready(origin):
    deadline = time.monotonic() + 30
    while time.monotonic() < deadline:
        try:
            return Client(origin)
        except OSError:
            time.sleep(.1)
    raise RuntimeError('Owned host did not become ready.')


def interactive(root, origin, store, client):
    backup = None
    while True:
        command = input().strip()
        if command == 'quit': return
        if command in ('event', 'tone'):
            publish_owned_event(root, command)
        elif command == 'state9': client.call(store, {STORE_KEY: {'count': 9}}, 'PUT')
        elif command == 'backup':
            request = urllib.request.Request(origin + '/api/recovery/backup', method='POST', headers={'X-TDSBLive-CSRF': client.nonce})
            backup = client.opener.open(request).read()
        elif command == 'restart':
            client.call('/api/application/restart', {}, 'POST'); time.sleep(1); client = ready(origin)
            assert client.call(store)[STORE_KEY]['count'] == 9
        elif command == 'restore' and backup is not None:
            request = urllib.request.Request(origin + '/api/recovery/validate', method='POST', data=backup, headers={'Content-Type': 'application/zip', 'X-TDSBLive-CSRF': client.nonce})
            preview = json.load(client.opener.open(request))
            client.call('/api/recovery/restore', {'id': preview['id'], 'confirm': True}, 'POST'); time.sleep(1); client = ready(origin)
            assert client.call(store)[STORE_KEY]['count'] == 3
        else:
            print('Unknown command or backup missing.', flush=True); continue
        print(f'Owned {command} completed.', flush=True)


def publish_owned_event(root, command):
    event = sample_event()
    event.update({'source': 'owned-g13-fixture', 'provenance': 'live', 'type': 'chat.message' if command == 'event' else 'owned.tone'})
    event['message']['text'] = 'Owned shim event rendered'
    insert_events(root / 'tdsblive.db', [event])
    with sqlite3.connect(root / 'tdsblive.db') as database:
        database.execute('UPDATE Outbox SET DeliveredAtTicks = NULL WHERE EventId = ?', (event['id'].upper(),))


def run(hold):
    with tempfile.TemporaryDirectory(prefix='tdsblive-g13-owned-') as directory:
        root = Path(directory)
        port = unused_port()
        root.joinpath('configuration.json').write_text(json.dumps({'server': {'host': '127.0.0.1', 'port': port}}))
        origin = f'http://127.0.0.1:{port}'
        process = subprocess.Popen(['dotnet', str(ROOT / 'src/ExtensionSuite.Host/bin/Release/net10.0/ExtensionSuite.Host.dll'),
            '--TDSBLive:DataDirectory', directory, '--TDSBLive:OpenEditor=false'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        try:
            wait_ready(process, origin)
            client = Client(origin)
            def upload(data, mime, filename):
                request = urllib.request.Request(origin + '/api/assets', method='POST', data=data,
                    headers={'Content-Type': mime, 'X-Asset-Filename': filename, 'X-TDSBLive-CSRF': client.nonce})
                return json.load(client.opener.open(request))['id']
            image = upload(scan_bytes(owned_png(), 'owned cyan PNG'), 'image/png', 'owned.png')
            video = upload(safe_read(ROOT / 'tests/fixtures/media/synthetic-pattern.webm'), 'video/webm', 'owned.webm')
            sound = io.BytesIO()
            with wave.open(sound, 'wb') as audio:
                audio.setnchannels(1); audio.setsampwidth(2); audio.setframerate(48000)
                audio.writeframes(b''.join(struct.pack('<h', int(3500 * math.sin(2 * math.pi * 660 * i / 48000))) for i in range(48000)))
            tone = upload(scan_bytes(sound.getvalue(), 'owned one-second tone'), 'audio/wav', 'owned-tone.wav')
            identifier = str(uuid.uuid4())
            custom = {
                'html': f'<h1>G13 owned local compatibility</h1><p id="load"></p><p id="message">Waiting for owned event</p><p id="state"></p><img src="{image}" style="width:100px;height:100px"><video src="{video}" muted loop style="width:320px;height:240px"></video>',
                'css': 'body { color:white; font:28px sans-serif; background:transparent; }',
                'javaScript': f'''const SE_API = SBX.enableStreamElements();
window.addEventListener('onWidgetLoad', e => document.getElementById('load').textContent = e.detail.fieldData.label);
window.addEventListener('onEventReceived', e => {{ if(e.detail.listener === 'message') document.getElementById('message').textContent = e.detail.event.message.text; }});
function refresh() {{ SE_API.store.get('owned').then(s => document.getElementById('state').textContent = 'Persisted local SE state '+(s?.count ?? 0)); }}
refresh(); SBX.on('sbx:session', s => {{ if(s.connected) refresh(); }});
SBX.on('owned.tone', () => {{ const sound = document.createElement('audio'); sound.setAttribute('src','{tone}'); document.body.appendChild(sound); }});''',
                'config': {'label': 'Opt-in lifecycle · local state · owned media'},
                'permissions': ['storage', 'chat', 'audio'], 'subscriptions': ['chat.message', 'owned.tone'], 'assetIds': [image, video, tone]
            }
            client.call('/api/overlays', {'id': 'g13-owned', 'name': 'Owned G13 compatibility', 'width': 1000, 'height': 700,
                'canvasEnabled': True, 'widgets': [{'id': identifier, 'kind': 'custom', 'name': 'Owned compatibility', 'width': 980, 'height': 680, 'x': 10, 'y': 10, 'custom': custom}]}, 'POST')
            store = f'/api/overlays/g13-owned/widgets/{identifier}/store'
            client.call(store, {STORE_KEY: {'count': 3}}, 'PUT')
            print(f'Owned G13 OBS URL: {origin}/overlay/g13-owned (1000 x 700)', flush=True)
            print('Host integrations disabled. Commands: event, tone, backup, state9, restart, restore, quit.', flush=True)
            if not hold:
                subprocess.run(['python', str(ROOT / 'tools/developer.py'), 'diagnostics', '--file', str(root / 'diagnostics.json'), '--host', origin], check=True)
                print('Owned aggregate diagnostic utility passed.', flush=True)
                return
            interactive(root, origin, store, client)
        finally:
            try:
                ready(origin).call('/api/application/quit', {}, 'POST')
                deadline = time.monotonic() + 20
                while time.monotonic() < deadline:
                    try: urllib.request.urlopen(origin + '/api/status', timeout=1); time.sleep(.1)
                    except OSError: break
                else: raise RuntimeError('Owned replacement host did not quit; preserve its temporary data.')
            finally:
                if process.poll() is None: process.terminate(); process.wait(timeout=20)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--hold', action='store_true')
    run(parser.parse_args().hold)
