"""Bounded local developer utilities; original input and backups stay private."""
import argparse
import json
import uuid
from datetime import datetime, timezone
from http.cookiejar import CookieJar
from pathlib import Path
from urllib.parse import urlsplit
from urllib.request import Request, HTTPCookieProcessor, build_opener

from rumble_evidence import EvidenceError, safe_read, scan_bytes


BACKUP_PATH = '/api/recovery/backup'

def sample_event():
    now = datetime.now(timezone.utc).isoformat()
    return {
        'id': str(uuid.uuid7()), 'occurredAt': now, 'receivedAt': now,
        'source': 'developer', 'platform': 'sample', 'type': 'chat.message',
        'nativeType': 'owned.sample', 'dedupeKey': str(uuid.uuid7()),
        'user': {'displayName': 'Sample viewer'}, 'message': {'text': 'Owned sample message'},
        'provenance': 'simulation',
    }


class LocalHost:
    def __init__(self, url):
        parsed = urlsplit(url)
        if parsed.scheme != 'http' or parsed.hostname != '127.0.0.1' or parsed.username or parsed.password or parsed.path not in ('', '/') or parsed.query or parsed.fragment:
            raise ValueError('Utilities require an explicit loopback HTTP host.')
        self.url = url.rstrip('/')
        self.client = build_opener(HTTPCookieProcessor(CookieJar()))

    def request(self, path, value=None):
        headers = {}
        if value is not None or path == BACKUP_PATH:
            token = json.loads(self.request('/api/auth/csrf'))['requestToken']
            headers['X-TDSBLive-CSRF'] = token
            headers['Content-Type'] = 'application/json'
        body = None
        if value is not None:
            body = json.dumps(value).encode()
        elif path == BACKUP_PATH:
            body = b''
        with self.client.open(Request(self.url + path, data=body, headers=headers), timeout=30) as response:
            content = response.read(64 * 1024 * 1024 + 1)
        if len(content) > 64 * 1024 * 1024:
            raise ValueError('Response exceeds utility limit.')
        return content


def replay(host, path):
    if path.stat().st_size > 2 * 1024 * 1024:
        raise ValueError('Fixture exceeds utility limit.')
    data = json.loads(safe_read(path))
    events = data if isinstance(data, list) else [data]
    if not 1 <= len(events) <= 100:
        raise ValueError('Replay accepts 1–100 canonical events.')
    for event in events:
        if not isinstance(event, dict) or not all(key in event for key in ('type', 'source', 'platform', 'nativeType', 'dedupeKey', 'occurredAt')):
            raise ValueError('Expected canonical event fixture.')
    for event in events:
        host.request('/api/test-event', {'event': event, 'persist': False})
    return len(events)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['sample-event', 'event-replay', 'backup', 'diagnostics'])
    parser.add_argument('--file', type=Path, required=True)
    parser.add_argument('--host', help='Explicit disposable local host, e.g. http://127.0.0.1:18474')
    args = parser.parse_args()
    try:
        if args.command == 'event-replay':
            count = replay(LocalHost(args.host or ''), args.file)
            print(f'Published {count} isolated preview events; no persistence or live actions.')
            return
        if args.file.exists():
            raise ValueError('Output exists; choose a new path.')
        if args.command == 'sample-event':
            content = scan_bytes((json.dumps(sample_event(), indent=2) + '\n').encode(), 'owned sample')
        else:
            host = LocalHost(args.host or '')
            content = host.request(BACKUP_PATH if args.command == 'backup' else '/api/diagnostics/export')
            if args.command == 'diagnostics':
                content = scan_bytes(content, 'aggregate diagnostics')
        with args.file.open('xb') as output:
            output.write(content)
        print('Saved private backup.' if args.command == 'backup' else 'Saved scanner-approved output.')
    except (EvidenceError, ValueError, OSError, KeyError, TypeError):
        parser.exit(1, 'Utility refused input or request. Check bounds, local host and secrets scan; no source values are printed.\n')


if __name__ == '__main__':
    main()
