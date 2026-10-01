#!/usr/bin/env python3
"""Isolated G07 HTTP/process qualification. Never uses operator data or outgoing automation.

Default: dated manual fixture rate, suitable for deterministic Windows CI.
--live-fx: additionally verifies the actual fixed Frankfurter provider through the host.
Only currency/date reach that external provider; all supporter data is owned fixture data.
"""
import argparse
from contextlib import closing
from datetime import datetime, timezone
from decimal import Decimal, ROUND_HALF_UP
import http.cookiejar
import json
from pathlib import Path
import secrets
import sqlite3
import subprocess
import tempfile
import time
import urllib.request
import uuid

from qualify_foundation import unused_port, wait_ready
from rumble_evidence import ROOT


class Client:
    def __init__(self, origin):
        self.origin = origin
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        # Antiforgery nonce stays in memory; never print it or retain it in reports.
        self.nonce = self.call('/api/auth/csrf')['requestToken']

    def call(self, route, body=None, method=None):
        headers = {}
        if body is not None:
            headers = {'Content-Type': 'application/json', 'X-TDSBLive-CSRF': self.nonce}
        request = urllib.request.Request(self.origin + route, method=method,
            data=None if body is None else json.dumps(body).encode(), headers=headers)
        with self.opener.open(request, timeout=45) as response:
            data = response.read()
            return json.loads(data) if data else None


def event_id():
    bits = (int(time.time() * 1000) & ((1 << 48) - 1)) << 80
    bits |= 7 << 76 | secrets.randbits(12) << 64 | 2 << 62 | secrets.randbits(62)
    return str(uuid.UUID(int=bits))


def owned_event(native_id, platform, support):
    return {'id': event_id(), 'source': 'owned-process-fixture', 'platform': platform,
        'type': 'support.donation', 'nativeType': 'Fixture.Support', 'nativeId': native_id,
        'dedupeKey': 'owned:' + native_id, 'occurredAt': '2026-01-05T12:00:00Z',
        'receivedAt': datetime.now(timezone.utc).isoformat(), 'provenance': 'live',
        'user': {'platformUserId': 'owned-giver', 'displayName': 'Owned fixture'}, 'support': support}


def insert_events(database, events):
    # Deliberately pre-acknowledged outbox rows prove financial catch-up is independent of automation delivery.
    with closing(sqlite3.connect(database, timeout=10)) as connection:
        connection.execute('PRAGMA foreign_keys=ON')
        for event in events:
            identifier = event['id'].upper()
            occurred = utc_ticks(event['occurredAt'])
            received = utc_ticks(event['receivedAt'])
            connection.execute('INSERT INTO Events (Id, Source, Provenance, DedupeKey, Type, OccurredAtTicks, Json) VALUES (?, ?, ?, ?, ?, ?, ?)',
                (identifier, event['source'], 'Live', event['dedupeKey'], event['type'], occurred, json.dumps(event)))
            connection.execute('INSERT INTO Outbox (EventId, CreatedAtTicks, DeliveredAtTicks) VALUES (?, ?, ?)', (identifier, received, received))
        connection.commit()


def utc_ticks(value):
    delta = datetime.fromisoformat(value.replace('Z', '+00:00')).astimezone(timezone.utc) - datetime(1, 1, 1, tzinfo=timezone.utc)
    return delta.days * 864000000000 + delta.seconds * 10000000 + delta.microseconds * 10


def wait_projected(client, expected_receipts):
    deadline = time.monotonic() + 30
    while time.monotonic() < deadline:
        status = client.call('/api/financial/diagnostics')
        if status['pending'] == 0 and status['processed'] == expected_receipts:
            return
        time.sleep(.1)
    raise RuntimeError('Owned financial projection did not reach its expected durable receipt count')


def qualify(configuration, live_fx):
    dll = ROOT / f'src/ExtensionSuite.Host/bin/{configuration}/net10.0/ExtensionSuite.Host.dll'
    if not dll.exists():
        raise RuntimeError('Build the selected host configuration first')
    with tempfile.TemporaryDirectory(prefix='tdsblive-g07-process-') as directory:
        root = Path(directory)
        port = unused_port()
        (root / 'configuration.json').write_text(json.dumps({'server': {'host': '127.0.0.1', 'port': port}}))
        origin = f'http://127.0.0.1:{port}'
        command = ['dotnet', str(dll), '--TDSBLive:DataDirectory', directory]
        process = None

        def start():
            child = subprocess.Popen(command, cwd=ROOT / 'src/ExtensionSuite.Host', stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            try:
                wait_ready(child, origin)
                return child, Client(origin)
            except BaseException:
                if child.poll() is None:
                    child.terminate(); child.wait(timeout=10)
                raise

        try:
            process, client = start()
            assert client.call('/api/financial/rules') == []
            client.call('/api/financial/settings', {'timeZone': 'America/Chicago', 'currentStreamStartUtc': '2026-01-05T12:00:00Z', 'version': 0}, 'PUT')
            client.call('/api/financial/rules', {'platform': 'twitch', 'type': 'bits', 'tier': '', 'usdMinorPerUnit': '1', 'expectedVersion': 0}, 'PUT')
            if not live_fx:
                client.call('/api/financial/rates/override', {'currency': 'EUR', 'date': '2026-01-05', 'usdPerNativeUnit': '1.17'}, 'PUT')
            lookup = client.call('/api/financial/rates/lookup', {'currency': 'EUR', 'date': '2026-01-05'}, 'POST')
            assert lookup['available'], 'Real host currency lookup was unavailable'
            rate = lookup['rate']
            assert rate['currency'] == 'EUR' and rate['requestedDate'] == '2026-01-05'
            assert Decimal(rate['usdPerNativeUnit']) > 0
            if live_fx:
                assert rate['provider'] == 'frankfurter:v2:blended'
            original_usd = str(int((Decimal('1000') * Decimal(rate['usdPerNativeUnit'])).quantize(Decimal('1'), rounding=ROUND_HALF_UP)))
            process.terminate(); process.wait(timeout=10)
            items = [owned_event('owned-eur', 'youtube', {'kind': 'donation', 'quantity': 1,
                'nativeMoney': {'amountMinor': 1000, 'currency': 'EUR', 'minorUnitDigits': 2}}),
                owned_event('owned-bits', 'twitch', {'kind': 'bits', 'quantity': 500})]
            insert_events(root / 'tdsblive.db', items)
            process, client = start()
            wait_projected(client, 2)
            page = client.call('/api/financial/ledger')
            assert page['totalCount'] == 2
            original = {entry['nativeEventId']: entry for entry in page['items']}
            assert original['owned-eur']['usdAmountMinor'] == original_usd
            assert original['owned-eur']['fxProvider'] == rate['provider']
            assert original['owned-eur']['fxRateDate'] == rate['rateDate']
            assert original['owned-eur']['estimated'] == rate['estimated']
            assert original['owned-bits']['usdAmountMinor'] == '500'
            assert original['owned-bits']['valuationMethod'] == 'configured_nominal' and original['owned-bits']['estimated']
            assert len(client.call('/api/financial/totals?period=current-stream')['supporters']) == 2
            client.call('/api/financial/rates/override', {'currency': 'EUR', 'date': '2026-01-05', 'usdPerNativeUnit': '2'}, 'PUT')
            client.call('/api/financial/rules', {'platform': 'twitch', 'type': 'bits', 'tier': '', 'usdMinorPerUnit': '2', 'expectedVersion': 1}, 'PUT')
            process.kill(); process.wait(timeout=10)  # Crash only this isolated child after persistent maintenance changes.
            with closing(sqlite3.connect(root / 'tdsblive.db')) as database:
                assert database.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
            process, client = start()
            wait_projected(client, 2)
            assert client.call('/api/financial/settings')['timeZone'] == 'America/Chicago'
            after_restart = {entry['nativeEventId']: entry for entry in client.call('/api/financial/ledger')['items']}
            assert {key: row['usdAmountMinor'] for key, row in original.items()} == {key: row['usdAmountMinor'] for key, row in after_restart.items()}
            duplicate = owned_event('owned-eur', 'youtube', {'kind': 'donation', 'quantity': 1,
                'nativeMoney': {'amountMinor': 9000, 'currency': 'EUR', 'minorUnitDigits': 2}})
            duplicate['dedupeKey'] = 'owned:alternate-receipt'
            insert_events(root / 'tdsblive.db', [duplicate])
            wait_projected(client, 3)
            assert client.call('/api/financial/ledger')['totalCount'] == 2
            refs = [{'id': row['id'], 'version': row['version']} for row in after_restart.values()]
            result = client.call('/api/financial/reconcile', {'selected': refs}, 'POST')
            assert all(row['outcome'] == 'reconciled' for row in result)
            assert all(row['outcome'] == 'conflict' for row in client.call('/api/financial/reconcile', {'selected': refs}, 'POST'))
            totals = client.call('/api/financial/totals?period=custom&start=2026-01-05&endExclusive=2026-01-06')['supporters']
            assert sum(int(row['usdAmountMinor']) for row in totals) == 3000
            synthetic = owned_event('owned-simulation', 'youtube', {'kind': 'donation', 'quantity': 1,
                'nativeMoney': {'amountMinor': 999999, 'currency': 'USD', 'minorUnitDigits': 2}})
            simulation = client.call('/api/test-event', {'event': synthetic, 'persist': True}, 'POST')
            assert simulation['provenance'] == 'simulation' and not simulation['liveActionsAllowed']
            wait_projected(client, 3)
            assert client.call('/api/financial/ledger')['totalCount'] == 2
            print('G07 process qualification passed: acknowledged catch-up, exact HTTP amounts, settings, real crash/restart, frozen history, native-ID dedupe, explicit reconciliation, stale-version conflicts and simulation isolation')
            print('FX qualification source: ' + ('live Frankfurter through host' if live_fx else 'dated manual fixture through host'))
        finally:
            if process is not None and process.poll() is None:
                process.terminate(); process.wait(timeout=10)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--configuration', choices=['Debug', 'Release'], default='Release')
    parser.add_argument('--live-fx', action='store_true')
    options = parser.parse_args()
    qualify(options.configuration, options.live_fx)
