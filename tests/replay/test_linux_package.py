"""Owned fixtures for distribution admission, isolation and executable tar output."""
import hashlib
import io
import json
from pathlib import Path
import stat
import sys
import tarfile
import tempfile
import unittest
from unittest.mock import patch
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'tools'))
from build_linux_package import archive_members, extract_windows, verify_backend, verify_checksum, write_archive


def archive(*entries):
    output = io.BytesIO()
    with zipfile.ZipFile(output, 'w') as zipped:
        for name, data in entries:
            # ZipInfo normalizes the host separator on Windows. Preserve the
            # actual malicious member bytes so admission sees the same archive
            # on every test platform.
            entry = zipfile.ZipInfo(name)
            entry.filename = entry.orig_filename = name
            zipped.writestr(entry, data)
    output.seek(0)
    return zipfile.ZipFile(output)


class LinuxPackageTests(unittest.TestCase):
    def test_archive_paths_cannot_escape_or_collide_and_private_content_is_rejected(self):
        for name in ('../outside', '/outside', 'C:/outside', 'folder\\outside', '.git/config',
                     'private/.secrets/key', 'node_modules/file', 'configuration.json', 'tdsblive.db',
                     'data.dpapi', 'capture.log', 'references/original'):
            with self.subTest(name=name), archive((name, 'owned marker')) as zipped:
                with self.assertRaises(ValueError):
                    archive_members(zipped)
        for first, second in (('same', 'SAME'), ('a//b', 'a/b'), ('folder/', 'folder')):
            with self.subTest(pair=(first, second)), archive((first, ''), (second, '')) as zipped:
                with self.assertRaises(ValueError):
                    archive_members(zipped)

    def test_links_encryption_and_expansion_limits_fail_before_extraction(self):
        with archive(('owned', 'marker')) as zipped:
            entry = zipped.infolist()[0]
            entry.external_attr = (stat.S_IFLNK | 0o777) << 16
            with self.assertRaises(ValueError):
                archive_members(zipped)
            entry.external_attr = 0
            entry.flag_bits |= 1
            with self.assertRaises(ValueError):
                archive_members(zipped)
            entry.flag_bits = 0
            entry.file_size = 2 * 1024 * 1024 * 1024 + 1
            with self.assertRaises(ValueError):
                archive_members(zipped)

    def test_extract_scans_decompressed_files_before_returning_and_refuses_existing_destination(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / 'owned.zip'
            with zipfile.ZipFile(source, 'w') as zipped:
                zipped.writestr('TDSBLive.exe', 'owned marker; never executed')
                zipped.writestr('folder/owned.txt', 'owned marker')
            target = root / 'backend'
            def scanned(path):
                self.assertEqual(target, path)
                self.assertTrue((path / 'TDSBLive.exe').is_file())
            with patch('build_linux_package.scan', side_effect=scanned) as scanner:
                extract_windows(source, target)
                scanner.assert_called_once_with(target)
            with self.assertRaises(ValueError):
                extract_windows(source, target)
            with patch('build_linux_package.scan', side_effect=ValueError('owned scan failure')):
                with self.assertRaises(ValueError):
                    extract_windows(source, root / 'rejected')

    def test_checksum_must_match_exactly_one_plain_filename(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / 'owned.zip'
            source.write_bytes(b'owned archive marker')
            digest = hashlib.sha256(source.read_bytes()).hexdigest()
            correct = f'{digest}  owned.zip\n'.encode()
            self.assertEqual(digest, verify_checksum(source, correct))
            for invalid in (correct + correct, f'{digest}  other.zip\n'.encode(),
                            f'{"0" * 64}  owned.zip\n'.encode(), f'{digest}  ../owned.zip\n'.encode(), b'bad'):
                with self.subTest(checksum=invalid), self.assertRaises(ValueError):
                    verify_checksum(source, invalid)

    def test_backend_requires_same_commit_version_platform_and_complete_files(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            required = ('TDSBLive.exe', 'TDSBLive.dll', 'ExtensionSuite.DesktopControl.dll',
                        'desktop/TDSBLive.Desktop.exe', 'wwwroot/editor/index.html', 'wwwroot/runtime/index.html',
                        'guide/Home.html', 'LICENSE.txt', 'licenses/Inter-OFL.txt', 'licenses/Avalonia-MIT.txt')
            for name in required:
                target = root / name
                target.parent.mkdir(parents=True, exist_ok=True)
                target.touch()
            marker = {'formatVersion': 1, 'version': '1.0.1', 'sourceCommit': 'owned-commit', 'target': 'win-x64'}
            path = root / 'TDSBLive.package.json'
            path.write_text(json.dumps(marker))
            # Known owned bytes only; actual CLI reads always use the scanner.
            with patch('build_linux_package.safe_read', side_effect=lambda source: source.read_bytes()):
                verify_backend(root, '1.0.1', 'owned-commit')
                for key, wrong in (('sourceCommit', 'other'), ('version', '1.0.0'),
                                   ('target', 'linux-x64'), ('formatVersion', True)):
                    path.write_text(json.dumps(marker | {key: wrong}))
                    with self.subTest(key=key), self.assertRaises(ValueError):
                        verify_backend(root, '1.0.1', 'owned-commit')
                path.write_text(json.dumps(marker))
                (root / 'wwwroot/runtime/index.html').unlink()
                with self.assertRaises(ValueError):
                    verify_backend(root, '1.0.1', 'owned-commit')

    @unittest.skipUnless(sys.platform == 'linux', 'Linux executable tar modes')
    def test_tar_preserves_executable_mode_with_fixed_owner_timestamp_and_top_directory(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / 'TDSBLive-1.0.1-linux-x64-wine'
            source.mkdir()
            executable = source / 'TDSBLive'
            executable.write_text('owned marker; never executed')
            executable.chmod(0o755)
            first = root / 'first.tar.gz'
            second = root / 'second.tar.gz'
            write_archive(source, first, 123456)
            write_archive(source, second, 123456)
            self.assertEqual(first.read_bytes(), second.read_bytes())
            with tarfile.open(first) as archive_file:
                item = archive_file.getmember(source.name + '/TDSBLive')
                self.assertEqual(0o755, item.mode)
                self.assertEqual(0, item.uid)
                self.assertEqual(0, item.gid)
                self.assertEqual(123456, item.mtime)
                self.assertTrue(item.isfile())
                self.assertFalse(any(member.name.startswith('/') or '..' in Path(member.name).parts for member in archive_file))


if __name__ == '__main__':
    unittest.main()
