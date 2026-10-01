#!/usr/bin/env python3
"""Isolated real-process G02 crash/restart, HTTP and contract qualification.

No production data or live integration calls. Child processes use temporary data.
Only metadata is printed. Credentials/nonces never enter output or artifacts.
"""
import argparse
from contextlib import closing
from datetime import datetime, timezone
import http.cookiejar
import json
from pathlib import Path
import socket
import sqlite3
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

from rumble_evidence import ROOT, safe_read, scan_bytes


def unused_port():
    with socket.socket() as listener:
        listener.bind(('127.0.0.1', 0))
        return listener.getsockname()[1]


def wait_ready(process, origin):
    deadline = time.monotonic() + 30
    while time.monotonic() < deadline:
        if process.poll() is not None:
            raise RuntimeError('Isolated foundation process exited before readiness')
        try:
            with urllib.request.urlopen(origin + '/api/status', timeout=1) as response:
                if response.status == 200:
                    return
        except (urllib.error.URLError, TimeoutError):
            time.sleep(0.1)
    raise RuntimeError('Isolated foundation readiness timed out')


def qualify():
    dll = ROOT / 'src/ExtensionSuite.Host/bin/Release/net10.0/ExtensionSuite.Host.dll'
    if not dll.exists():
        raise RuntimeError('Build the Release host first')
    with tempfile.TemporaryDirectory(prefix='tdsblive-g02-process-') as directory:
        root = Path(directory)
        port = unused_port()
        (root / 'configuration.json').write_text(json.dumps({'server': {'host': '127.0.0.1', 'port': port}}))
        origin = f'http://127.0.0.1:{port}'
        command = ['dotnet', str(dll), '--TDSBLive:DataDirectory', directory]
        process = subprocess.Popen(command, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        try:
            wait_ready(process, origin)
            for route in ['/editor', '/login']:
                with urllib.request.urlopen(origin + route) as response:
                    assert response.status == 200 and b'type="module"' in response.read()
            with urllib.request.urlopen(origin + '/api/openapi/v1.json') as response:
                actual = json.loads(scan_bytes(response.read(), 'generated OpenAPI contract'))
            actual.pop('servers', None)  # Runtime listener origin is not a schema change.
            expected = json.loads(safe_read(ROOT / 'docs/contracts/openapi.json'))
            expected.pop('servers', None)
            assert actual == expected, 'OpenAPI snapshot drift; refresh and regenerate frontend types'
            cookies = http.cookiejar.CookieJar()
            client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cookies))
            csrf = json.load(client.open(origin + '/api/auth/csrf'))['requestToken']
            event = {'source': 'internal', 'platform': 'system', 'type': 'qualification.test',
                     'nativeType': 'qualification.test', 'dedupeKey': 'synthetic-runtime',
                     'occurredAt': datetime.now(timezone.utc).isoformat(),
                     'raw': {'token': 'synthetic-private-placeholder', 'unknown': True}}
            request = urllib.request.Request(origin + '/api/test-event', method='POST',
                data=json.dumps({'event': event, 'persist': True}).encode(),
                headers={'Content-Type': 'application/json', 'X-TDSBLive-CSRF': csrf})
            result = json.load(client.open(request))
            assert result['persisted'] and not result['liveActionsAllowed']
            process.kill()  # Intentional crash of this isolated child only.
            process.wait(timeout=10)
            with closing(sqlite3.connect(root / 'tdsblive.db')) as database:
                assert database.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
                assert database.execute('SELECT COUNT(*) FROM Events').fetchone()[0] == 1
                stored = database.execute('SELECT Json FROM Events').fetchone()[0]
                assert 'synthetic-private-placeholder' not in stored
                assert json.loads(stored)['provenance'] == 'simulation'
            process = subprocess.Popen(command, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            wait_ready(process, origin)
            diagnostics = json.load(urllib.request.urlopen(origin + '/api/diagnostics'))
            assert diagnostics['eventCount'] == 1
            assert json.load(urllib.request.urlopen(origin + '/api/events')) == []
            print('G02 process qualification passed: HTTP shells, contract drift, crash recovery, redaction, restart and test isolation')
        finally:
            if process.poll() is None:
                process.terminate()
                process.wait(timeout=10)


if __name__ == '__main__':
    argparse.ArgumentParser(description=__doc__).parse_args()
    qualify()
