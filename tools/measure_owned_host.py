"""Linux-only measurement of the disposable G13 qualification host."""
import argparse
import json
import os
from pathlib import Path
import sqlite3
import time

from developer import sample_event
from qualify_financial import insert_events


def sample(pid):
    fields = Path(f'/proc/{pid}/stat').read_text().rsplit(')', 1)[1].split()
    cpu = (int(fields[11]) + int(fields[12])) / os.sysconf('SC_CLK_TCK')
    rss = int(fields[21]) * os.sysconf('SC_PAGE_SIZE')
    peak = next(int(line.split()[1]) * 1024 for line in Path(f'/proc/{pid}/status').read_text().splitlines() if line.startswith('VmHWM:'))
    return time.monotonic(), cpu, rss, peak


def measure(pid, duration, database=None):
    start = sample(pid); deadline = start[0] + duration; next_event = start[0]; count = 0; peak = start[3]
    while time.monotonic() < deadline:
        if database is not None and time.monotonic() >= next_event:
            event = sample_event(); event.update({'source': 'owned-g13-performance', 'provenance': 'live'})
            insert_events(database, [event])
            with sqlite3.connect(database) as connection:
                connection.execute('UPDATE Outbox SET DeliveredAtTicks = NULL WHERE EventId = ?', (event['id'].upper(),))
            count += 1; next_event += .5
        peak = max(peak, sample(pid)[3]); time.sleep(.05)
    end = sample(pid); elapsed = end[0] - start[0]
    return {'seconds': round(elapsed, 2), 'cpuPercentOneCore': round(100 * (end[1] - start[1]) / elapsed, 3),
            'peakRssBytes': max(peak, end[3]), 'events': count}


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--pid', type=int, required=True)
    args = parser.parse_args()
    command = Path(f'/proc/{args.pid}/cmdline').read_bytes().decode().split('\0')
    if '--TDSBLive:DataDirectory' not in command or not any(value.endswith('ExtensionSuite.Host.dll') for value in command):
        parser.error('Only the owned managed host may be measured.')
    data = Path(command[command.index('--TDSBLive:DataDirectory') + 1]).resolve()
    if data.parent != Path('/tmp') or not data.name.startswith('tdsblive-g13-owned-'):
        parser.error('Only the disposable G13 host may receive workload fixtures.')
    configuration = json.loads((data / 'configuration.json').read_text())
    if any(configuration.get(name, {}).get('enabled', False) for name in ('rumble', 'streamerBot', 'speakerBot')):
        parser.error('This baseline requires integrations disabled.')
    idle = measure(args.pid, 60); print(json.dumps({'idle': idle}), flush=True)
    chat = measure(args.pid, 180, data / 'tdsblive.db')
    print(json.dumps({'environment': 'Linux managed .NET; one owned OBS custom source; integrations disabled', 'idle': idle, 'chat': chat,
                      'limitation': 'Available-environment baseline only; not native Windows, Wine or enabled-Rumble performance qualification.'}), flush=True)


if __name__ == '__main__': main()
