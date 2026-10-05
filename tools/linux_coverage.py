#!/usr/bin/env python3
"""Move unchanged Linux OpenCover evidence between checkouts of the same commit.

Only source file paths change. Visit counts, methods, branches and summaries stay
as measured. The receiving Windows analysis requires this run's exact checkout.
"""
import argparse
import json
from pathlib import Path, PurePosixPath
import subprocess
import xml.etree.ElementTree as ET

from rumble_evidence import safe_read

ROOT = Path(__file__).resolve().parents[1]
MAXIMUM_BYTES = 16 * 1024 * 1024


def transform_report(data, root, exporting):
    if len(data) > MAXIMUM_BYTES or b'<!DOCTYPE' in data or b'<!ENTITY' in data:
        raise ValueError('Invalid or oversized coverage XML')
    tree = ET.ElementTree(ET.fromstring(data))
    if tree.getroot().tag != 'CoverageSession':
        raise ValueError('Expected OpenCover coverage session')
    files = tree.findall('./Modules/Module/Files/File')
    if not files:
        raise ValueError('Coverage report contains no source files')
    for source in files:
        value = source.attrib['fullPath']
        if exporting:
            relative = Path(value).resolve().relative_to(root.resolve()).as_posix()
        else:
            relative = value
        parts = PurePosixPath(relative).parts
        if (not parts or parts[0] != 'src' or '..' in parts or '\\' in relative
                or ':' in relative or not relative.endswith('.cs')):
            raise ValueError('Coverage source must be a repository production C# file')
        destination = root.joinpath(*parts)
        if not destination.is_file() or not destination.resolve().is_relative_to(root.resolve()):
            raise ValueError('Coverage source is absent or outside this checkout')
        source.set('fullPath', relative if exporting else str(destination.resolve()))
    return tree


def export_reports(reports, output, root, commit):
    reports = sorted(reports.glob('**/coverage.opencover.xml'))
    if not reports:
        raise ValueError('Linux test run produced no OpenCover report')
    if output.exists():
        raise ValueError('Use a fresh coverage export directory')
    # Validate every scanned input before publishing any report.
    trees = [transform_report(safe_read(report), root, True) for report in reports]
    output.mkdir(parents=True)
    for index, tree in enumerate(trees):
        directory = output / str(index)
        directory.mkdir()
        tree.write(directory / 'coverage.opencover.xml', encoding='utf-8', xml_declaration=True)
    (output / 'manifest.json').write_text(json.dumps({'version': 1, 'commit': commit,
                                                    'reports': len(trees)}) + '\n')


def import_reports(directory, root, commit):
    manifest = json.loads(safe_read(directory / 'manifest.json'))
    if manifest.get('version') != 1 or manifest.get('commit') != commit:
        raise ValueError('Linux coverage must come from this exact checkout commit')
    reports = sorted(directory.glob('**/coverage.opencover.xml'))
    if not reports or len(reports) != manifest.get('reports'):
        raise ValueError('Linux coverage artifact is incomplete')
    trees = [transform_report(safe_read(report), root, False) for report in reports]
    for report, tree in zip(reports, trees):
        tree.write(report, encoding='utf-8', xml_declaration=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    export = sub.add_parser('export')
    export.add_argument('--reports', type=Path, required=True)
    export.add_argument('--output', type=Path, required=True)
    load = sub.add_parser('import')
    load.add_argument('--directory', type=Path, required=True)
    args = parser.parse_args()
    commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    if args.command == 'export':
        export_reports(args.reports, args.output, ROOT, commit)
    else:
        import_reports(args.directory, ROOT, commit)
    print('Linux OpenCover evidence verified for this exact checkout; measured counts preserved.')


if __name__ == '__main__':
    main()
