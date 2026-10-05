"""Cross-platform report admission and unchanged measured coverage evidence."""
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'tools'))
from linux_coverage import export_reports, import_reports, transform_report


def report(filename):
    root = ET.Element('CoverageSession')
    module = ET.SubElement(ET.SubElement(root, 'Modules'), 'Module')
    ET.SubElement(ET.SubElement(module, 'Files'), 'File', uid='1', fullPath=str(filename))
    points = ET.SubElement(ET.SubElement(ET.SubElement(ET.SubElement(module, 'Classes'), 'Class'), 'Methods'), 'Method')
    ET.SubElement(ET.SubElement(points, 'SequencePoints'), 'SequencePoint', vc='7', sl='42', fileid='1')
    ET.SubElement(ET.SubElement(points, 'BranchPoints'), 'BranchPoint', vc='0', sl='42', fileid='1')
    return ET.tostring(root)


class LinuxCoverageTests(unittest.TestCase):
    def test_round_trip_remaps_only_source_paths_and_preserves_counts(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            old = root / 'linux checkout'
            new = root / 'windows checkout'
            for checkout in (old, new):
                (checkout / 'src').mkdir(parents=True)
                (checkout / 'src' / 'Owned.cs').touch()
            source = old / 'reports'
            source.mkdir()
            (source / 'coverage.opencover.xml').write_bytes(report(old / 'src' / 'Owned.cs'))
            output = root / 'artifact'
            # Owned fixture bytes only. Actual CLI reads always use safe_read's scanner.
            with patch('linux_coverage.safe_read', side_effect=lambda path: path.read_bytes()):
                export_reports(source, output, old, 'owned-commit')
                import_reports(output, new, 'owned-commit')
            tree = ET.parse(output / '0' / 'coverage.opencover.xml')
            self.assertEqual(str((new / 'src' / 'Owned.cs').resolve()), tree.find('.//File').get('fullPath'))
            self.assertEqual('7', tree.find('.//SequencePoint').get('vc'))
            self.assertEqual('0', tree.find('.//BranchPoint').get('vc'))

    def test_wrong_commit_and_incomplete_artifacts_are_rejected_before_rewrite(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest = root / 'manifest.json'
            for values in ({'version': 1, 'commit': 'another', 'reports': 1},
                           {'version': 1, 'commit': 'same', 'reports': 1},
                           {'version': 99, 'commit': 'same', 'reports': 1}):
                manifest.write_text(json.dumps(values))
                with patch('linux_coverage.safe_read', side_effect=lambda path: path.read_bytes()):
                    with self.assertRaises(ValueError):
                        import_reports(root, root, 'same')

    def test_source_paths_cannot_escape_the_checkout_or_cover_tests(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'src').mkdir()
            (root / 'src' / 'Owned.cs').touch()
            for invalid in ('../src/Owned.cs', '/src/Owned.cs', 'src/../Owned.cs',
                            'tests/Owned.cs', 'src/Missing.cs', 'src/Owned.py', 'src\\Owned.cs', 'src/C:Owned.cs'):
                with self.subTest(path=invalid), self.assertRaises(ValueError):
                    transform_report(report(invalid), root, False)

    def test_xml_entities_empty_reports_and_oversized_inputs_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            for invalid in (b'<CoverageSession/>', b'<Other/>', b'<!DOCTYPE CoverageSession><CoverageSession/>',
                            b'<!ENTITY x "owned"><CoverageSession/>', b'x' * (16 * 1024 * 1024 + 1)):
                with self.assertRaises(ValueError):
                    transform_report(invalid, Path(directory), False)

    def test_export_requires_reports_and_a_fresh_destination(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with self.assertRaises(ValueError):
                export_reports(root, root / 'export', root, 'same')
            (root / 'coverage.opencover.xml').touch()
            with self.assertRaises(ValueError):
                export_reports(root, root, root, 'same')


if __name__ == '__main__':
    unittest.main()
