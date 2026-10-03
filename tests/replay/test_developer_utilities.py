"""Local-only utility boundaries with disposable owned fixtures."""
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'tools'))
from developer import LocalHost, replay, sample_event


class DeveloperUtilityTests(unittest.TestCase):
    def test_sample_has_uuid7_and_owned_preview_data(self):
        import uuid
        sample = sample_event()
        self.assertEqual(7, uuid.UUID(sample['id']).version)
        self.assertEqual('simulation', sample['provenance'])
        self.assertEqual('Sample viewer', sample['user']['displayName'])

    def test_only_explicit_loopback_http_hosts_are_allowed(self):
        for address in ['https://127.0.0.1:18474', 'http://example.com', 'http://127.0.0.1/path',
                        'http://viewer:password@127.0.0.1', 'http://127.0.0.1?private=value', 'http://localhost', '']:
            with self.assertRaises(ValueError):
                LocalHost(address)
        self.assertEqual('http://127.0.0.1:18474', LocalHost('http://127.0.0.1:18474/').url)

    def test_file_replay_forces_nonpersistent_simulation_and_validates_before_dispatch(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory, 'owned.json')
            sample = sample_event()
            path.write_text(json.dumps([sample]))
            host = Mock()
            # Scanner behavior is separately enforced by safe_read; these owned
            # unit fixtures test admission and the HTTP request's isolation flag.
            with patch('developer.safe_read', return_value=path.read_bytes()):
                self.assertEqual(1, replay(host, path))
                host.request.assert_called_once_with('/api/test-event', {'event': sample, 'persist': False})
            host.reset_mock()
            for bad in [[sample, {}], [], [sample] * 101]:
                with patch('developer.safe_read', return_value=json.dumps(bad).encode()):
                    with self.assertRaises(ValueError):
                        replay(host, path)
                host.request.assert_not_called()


if __name__ == '__main__':
    unittest.main()
