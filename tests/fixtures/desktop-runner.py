#!/usr/bin/env python3
"""Owned transport test double. Does not run Wine or execute Windows code."""
import json
import os
import secrets
import socketserver
import sys
import threading
import time
from pathlib import Path

root = Path(__file__).parent
mode = (root / 'mode').read_text().strip()
(root / 'pid').write_text(str(os.getpid()))
# Store only nonsensitive launch metadata; bootstrap credentials stay in memory.
(root / 'launch.json').write_text(json.dumps({
    'arguments': sys.argv[1:], 'prefix': os.environ.get('WINEPREFIX'),
    'overrides': os.environ.get('WINEDLLOVERRIDES')}))
if sys.argv[1:] == ['wineboot.exe', '--init']:
    if mode == 'init-fails':
        sys.exit(23)
    (Path(os.environ['WINEPREFIX']) / 'drive_c').mkdir(parents=True)
    (root / 'initialized').write_text(os.environ['WINEDLLOVERRIDES'])
    sys.exit(0)

output_bootstrap = '--TDSBLive:DesktopBootstrap=output' in sys.argv[1:]
bootstrap = {'SessionToken': secrets.token_hex(32)} if output_bootstrap else json.loads(sys.stdin.readline())
(root / 'started').touch()
if mode == 'no-marker':
    time.sleep(30)
    sys.exit(0)
if mode == 'bad-marker':
    print('TDSBLIVE-DESKTOP 0', flush=True)
    sys.exit(23)

completed = threading.Event()
state = 'starting'
statuses = 0
lock = threading.Lock()

class Handler(socketserver.StreamRequestHandler):
    def handle(self):
        global state, statuses
        request = json.loads(self.rfile.readline(4096))
        if request.get('SessionToken') != bootstrap['SessionToken']:
            return
        command = request.get('Command')
        if command == 'wait':
            completed.wait(30)
            reply = {'State': state}
        elif command == 'status':
            with lock:
                statuses += 1
                if not completed.is_set():
                    state = 'stopped' if mode == 'not-running' else ('starting' if statuses < 3 else 'running')
            reply = {'State': state, 'EditorUrl': 'http://127.0.0.1:23456/editor'}
        else:
            state = 'restart-ready' if command == 'restart' else 'quit'
            reply = {'State': state, 'Accepted': True}
        self.wfile.write(json.dumps(reply).encode() + b'\n')
        self.wfile.flush()
        if command in ('restart', 'quit'):
            completed.set()

class Server(socketserver.ThreadingTCPServer):
    daemon_threads = True

with Server(('127.0.0.1', 0), Handler) as server:
    # Exercise simultaneous redirected pipes beyond the operating system buffer.
    sys.stderr.write('owned private fixture output\n' * 8192)
    sys.stderr.flush()
    sys.stdout.write('owned private fixture output\n' * 8192)
    if output_bootstrap:
        print('TDSBLIVE-DESKTOP-BOOTSTRAP ' + json.dumps({
            'Port': server.server_address[1], 'SessionToken': bootstrap['SessionToken']}), flush=True)
    else:
        print('TDSBLIVE-DESKTOP ' + str(server.server_address[1]), flush=True)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    completed.wait(30)
    server.shutdown()
    thread.join()
sys.exit(23 if mode == 'bad-exit' else 0)
