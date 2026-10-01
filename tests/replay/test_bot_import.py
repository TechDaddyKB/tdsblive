"""Qualification of the public import transport and output confinement."""
import base64
import gzip
import json
from pathlib import Path
import subprocess
import socket
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
from rumble_evidence import safe_read, scan_bytes
sys.path.insert(0, str(ROOT / 'tools/streamerbot'))
import install_native


class BotImportTests(unittest.TestCase):
    def test_native_scan_requires_explicit_clean_verdict_before_read(self):
        with tempfile.TemporaryDirectory() as directory:
            data = Path(directory); (data / 'actions.json').write_text('{}')
            with socket.socket() as listener:
                listener.bind(('127.0.0.1', 0)); port = listener.getsockname()[1]
            for verdict in (b'', b'Secret detected'):
                with patch.object(install_native.subprocess, 'run', return_value=subprocess.CompletedProcess([], 0, verdict)), patch.object(Path, 'read_bytes') as read:
                    with self.assertRaisesRegex(RuntimeError, 'no content was read'):
                        install_native.install(data / 'unused.sb', data, port, False)
                    read.assert_not_called()

    def test_native_import_preserves_unrelated_actions_bindings_and_private_backup(self):
        artifacts = ROOT / 'artifacts'; artifacts.mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(dir=artifacts) as directory:
            data = Path(directory); bundle = data / 'native.sb'; native = data / 'actions.json'
            subprocess.run([sys.executable, str(ROOT / 'tools/streamerbot/build_import.py'), str(bundle)], check=True)
            original = {'version': 1, 'actions': [{'id': 'unrelated', 'name': 'Existing operator action', 'group': 'Existing', 'triggers': []}], 'queues': [], 'unknown': 'preserved'}
            native.write_text(json.dumps(original))
            with socket.socket() as listener:
                listener.bind(('127.0.0.1', 0)); port = listener.getsockname()[1]
            command = [sys.executable, str(ROOT / 'tools/streamerbot/install_native.py'), str(bundle), '--data-directory', str(data), '--native-version', '1.0.7', '--streamer-port', str(port), '--bind-qualification-probe']
            subprocess.run(command, check=True, stdout=subprocess.DEVNULL)
            result = json.loads(safe_read(native))
            self.assertEqual(result['actions'][0], original['actions'][0]); self.assertEqual(result['unknown'], 'preserved')
            self.assertEqual(len(result['actions']), 4)
            self.assertEqual(len(result['queues']), 1)
            probe = next(a for a in result['actions'] if a['name'] == 'TDSBLive qualification probe')
            self.assertEqual(probe['triggers'][0]['eventName'], 'tdsblive.test.trigger')
            self.assertEqual(json.loads(safe_read(next(data.glob('actions.tdsblive-backup-*.json')))), original)
            subprocess.run(command, check=True, stdout=subprocess.DEVNULL)
            repeated = json.loads(safe_read(native))
            self.assertEqual(result, repeated)
            subprocess.run(command + ['--bind-rumble-qualification'], check=True, stdout=subprocess.DEVNULL)
            qualified = json.loads(safe_read(native))
            bindings = next(a for a in qualified['actions'] if a['name'] == 'TDSBLive qualification probe')['triggers']
            self.assertIn('tdsblive.rumble.health', [t['eventName'] for t in bindings])
            self.assertNotIn('tdsblive.rumble.gift', [t['eventName'] for t in bindings])
            self.assertNotIn('tdsblive.rumble.subscription', [t['eventName'] for t in bindings])

    def test_native_import_refuses_a_listening_bot(self):
        with socket.socket() as listener:
            listener.bind(('127.0.0.1', 0)); listener.listen()
            result = subprocess.run([sys.executable, str(ROOT / 'tools/streamerbot/install_native.py'), 'unused.sb',
                '--data-directory', str(ROOT), '--native-version', '1.0.7', '--streamer-port', str(listener.getsockname()[1])],
                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False)
            self.assertNotEqual(result.returncode, 0)

    def test_rejects_output_outside_generated_artifacts(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory, 'public.sb')
            result = subprocess.run([sys.executable, str(ROOT / 'tools/streamerbot/build_import.py'), str(output)],
                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False)
            self.assertNotEqual(result.returncode, 0)
            self.assertFalse(output.exists())

    def test_import_is_deterministic_and_contains_only_explicit_actions(self):
        artifacts = ROOT / 'artifacts'; artifacts.mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(dir=artifacts) as directory:
            output = Path(directory, 'test.sb')
            command = [sys.executable, str(ROOT / 'tools/streamerbot/build_import.py'), str(output)]
            subprocess.run(command, check=True)
            first = safe_read(output)
            subprocess.run(command, check=True)
            self.assertEqual(first, safe_read(output))
            encoded = base64.b64decode(first, validate=True)
            self.assertEqual(encoded[:4], b'SBAE')
            document = json.loads(scan_bytes(gzip.decompress(encoded[4:]), 'decoded public bot import'))
            actions = document['data']['actions']
            self.assertEqual(len(actions), 3)
            self.assertEqual(len({action['id'] for action in actions}), 3)
            self.assertTrue(all(not action['triggers'] for action in actions))
            self.assertFalse(document['data']['commands'])
            self.assertFalse(document['data']['websocketServers'])
            self.assertIsNone(document['meta']['minimumVersion'])
            self.assertEqual(document['minimumVersion'], '0.2.4-beta.6')
            self.assertEqual(document['exportedFrom'], '1.0.7')


if __name__ == '__main__':
    unittest.main()
