"""Build a deterministic Streamer.bot import; output is a generated artifact."""
import argparse
import base64
import gzip
import json
import subprocess
from pathlib import Path
from uuid import UUID, uuid5

NAMESPACE = UUID('90f0339e-036b-446b-a6fa-07abc3c783d8')
ROOT = Path(__file__).resolve().parents[2]

def identifier(name):
    return str(uuid5(NAMESPACE, name))

def actions():
    queue = identifier('queue')
    result = []
    for name, source in [('TDSBLive bootstrap', 'TDSBLiveBootstrap.cs'), ('TDSBLive qualification probe', 'TDSBLiveQualificationProbe.cs'), ('TDSBLive explicit forwarder', 'TDSBLiveForwardEvent.cs')]:
        path = ROOT / 'integrations/streamerbot' / source
        subprocess.run(['sonar', 'analyze', 'secrets', str(path)], check=True)
        code = path.read_bytes()
        result.append({'id': identifier(name), 'queue': queue, 'enabled': True, 'excludeFromHistory': False, 'excludeFromPending': False,
            'name': name, 'group': 'TDSBLive', 'alwaysRun': False, 'randomAction': False, 'concurrent': False,
            'triggers': [], 'actions': [{'id': identifier(source), 'type': 99999, 'enabled': True, 'parentId': None, 'weight': 0, 'index': 0,
                'name': name, 'description': '', 'references': [], 'byteCode': base64.b64encode(code).decode(), 'precompile': True,
                'delayStart': False, 'saveResultToVariable': False, 'saveToVariable': ''}], 'collapsedGroups': []})
    return result

def build():
    return {'meta': {'name': 'TDSBLive G03 integrations', 'author': 'techdaddykb', 'version': '0.3.0',
        'description': 'Explicit bootstrap, isolated qualification probe, and forwarding template. No platform automation is bound by default.',
        'autoRunAction': None, 'minimumVersion': None}, 'data': {'actions': actions(),
        'queues': [{'id': identifier('queue'), 'name': 'TDSBLive', 'blocking': False}], 'commands': [], 'websocketServers': [], 'websocketClients': [], 'timers': []},
        # This is the import-format floor, not a claim of qualified runtime support.
        # Runtime qualification remains Streamer.bot 1.0.7. An author version gate
        # rejects the installed 1.0.7 build despite displaying the same version.
        'version': 10, 'exportedFrom': '1.0.7', 'minimumVersion': '0.2.4-beta.6'}

if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('output', type=Path); args = parser.parse_args()
    output = args.output.resolve()
    if not output.is_relative_to((ROOT / 'artifacts').resolve()) or output.suffix != '.sb':
        parser.error('Output must be an .sb file inside the repository artifacts directory')
    output.parent.mkdir(parents=True, exist_ok=True)
    payload = json.dumps(build(), separators=(',', ':')).encode()
    output.write_text(base64.b64encode(b'SBAE' + gzip.compress(payload, mtime=0)).decode(), encoding='ascii')
