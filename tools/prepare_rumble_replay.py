"""Scan decompressed public evidence before .NET replay tests read it."""
import gzip
from rumble_evidence import ROOT, FIXTURES, safe_read, scan_bytes

if __name__ == '__main__':
    decoded = scan_bytes(gzip.decompress(safe_read(FIXTURES / 'captured.jsonl.gz')), 'decompressed public capture')
    output = ROOT / 'artifacts/rumble-replay/captured.jsonl'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(decoded)
    print('Prepared scanner-approved decompressed replay fixture.')
