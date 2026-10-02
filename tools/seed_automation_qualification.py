"""Inject one owned event into an explicitly opted-in, isolated qualification DB.

Never used by simulation or production. All three inserts commit atomically.
"""
import argparse
from contextlib import closing
from datetime import datetime, timezone
import json
from pathlib import Path
import sqlite3

from qualify_financial import owned_event, utc_ticks


def seed(directory, platform, event_type):
    directory = directory.resolve()
    marker = directory / 'g09-owned-qualification'
    if not directory.name.startswith('tdsblive-g09-') or not marker.is_file():
        raise ValueError('Only explicitly marked isolated G09 qualification directories are allowed')
    if not (directory / 'tdsblive.db').is_file():
        raise ValueError('An existing isolated host database is required')
    if (platform, event_type) not in [('twitch', 'support.bits'), ('kofi', 'support.donation')]:
        raise ValueError('Qualification platform and event type must match')
    support = {'kind': 'bits', 'quantity': 100, 'giftRole': 'none'}
    if event_type == 'support.donation':
        support = {'kind': 'donation', 'quantity': 1, 'giftRole': 'none',
                   'nativeMoney': {'amountMinor': 500, 'currency': 'USD', 'minorUnitDigits': 2}}
    event = owned_event('g09-' + datetime.now(timezone.utc).isoformat(), platform, support)
    event['type'] = event_type
    event['occurredAt'] = event['receivedAt']
    event['message'] = {'text': 'TDSBLive owned G09 audio qualification'}
    event['automation'] = {'anonymous': False, 'messagePublic': True, 'language': 'en'}
    identifier = event['id'].upper()
    with closing(sqlite3.connect(directory / 'tdsblive.db', timeout=10)) as connection:
        connection.execute('PRAGMA foreign_keys=ON')
        with connection:
            connection.execute('INSERT INTO Events (Id, Source, Provenance, DedupeKey, Type, OccurredAtTicks, Json) VALUES (?, ?, ?, ?, ?, ?, ?)',
                               (identifier, event['source'], 'Live', event['dedupeKey'], event['type'], utc_ticks(event['occurredAt']), json.dumps(event)))
            connection.execute('INSERT INTO Outbox (EventId, CreatedAtTicks, DeliveredAtTicks) VALUES (?, ?, ?)',
                               (identifier, utc_ticks(event['receivedAt']), utc_ticks(event['receivedAt'])))
            connection.execute('INSERT INTO AutomationInbox (EventId, Processed) VALUES (?, 0)', (identifier,))
    print(event['id'])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('--platform', choices=['twitch', 'kofi'], default='twitch')
    parser.add_argument('--event-type', choices=['support.bits', 'support.donation'], default='support.bits')
    parser.add_argument('--execute-owned-live-test', action='store_true', required=True)
    args = parser.parse_args()
    seed(args.directory, args.platform, args.event_type)
