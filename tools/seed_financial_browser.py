#!/usr/bin/env python3
"""Seed owned support facts only in the browser qualifier's disposable database."""
import argparse
from pathlib import Path
import tempfile
from qualify_financial import insert_events, owned_event

def seed(directory):
    root = Path(directory).resolve()
    if root.parent != Path(tempfile.gettempdir()).resolve() or not root.name.startswith('tdsblive-browser-test-'):
        raise ValueError('Only a disposable browser qualification directory is supported')
    database = root / 'tdsblive.db'
    if not database.is_file():
        raise ValueError('The isolated host must create its schema first')
    facts = [('browser-bits', 'twitch', {'kind': 'bits', 'quantity': 100}),
        ('browser-donation', 'kofi', {'kind': 'donation', 'quantity': 1,
            'nativeMoney': {'amountMinor': 1250, 'currency': 'USD', 'minorUnitDigits': 2}}),
        ('browser-subscription', 'kick', {'kind': 'subscription', 'quantity': 1})]
    events = [owned_event(identifier, platform, support) for identifier, platform, support in facts]
    for event in events:
        event['type'] = 'support.' + event['support']['kind']
    insert_events(database, events)
    print('Seeded three owned support events in disposable browser data')

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory')
    seed(parser.parse_args().directory)
