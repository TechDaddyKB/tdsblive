#!/usr/bin/env python3
"""Reject missing/empty or low-coverage reports before SonarQube import.

These reports concern handwritten production code. Development evidence tooling,
tests and configuration are not production coverage targets.
"""
from pathlib import Path
import xml.etree.ElementTree as ET

from rumble_evidence import safe_read


def check_backend(root=Path('.')):
    reports = sorted((root / 'TestResults').glob('**/coverage.opencover.xml'))
    if not reports:
        raise ValueError('Missing backend OpenCover report')
    points = {}
    for report in reports:
        xml = ET.fromstring(safe_read(report))
        for module in xml.findall('.//Module'):
            files = {f.attrib['uid']: f.attrib['fullPath'] for f in module.findall('./Files/File')}
            for method in module.findall('.//Method'):
                for point in method.findall('./SequencePoints/SequencePoint'):
                    filename = files.get(point.attrib['fileid'], '')
                    if '/src/' not in filename.replace('\\', '/'):
                        continue
                    key = (filename, point.attrib['sl'], point.attrib.get('sc', ''))
                    points[key] = max(points.get(key, 0), int(point.attrib['vc']))
    if not points:
        raise ValueError('OpenCover reports contain no production sequence points')
    ratio = sum(v > 0 for v in points.values()) / len(points)
    if ratio < 0.8:
        raise ValueError(f'Backend coverage below 80%: {ratio:.1%}')
    return ratio


def check_frontend(root=Path('.')):
    content = safe_read(root / 'coverage/lcov.info').decode()
    found = hit = 0
    for record in content.split('end_of_record'):
        lines = record.splitlines()
        source = next((s[3:] for s in lines if s.startswith('SF:')), '')
        if '/src/' not in source.replace('\\', '/') or '.test.' in source:
            continue
        for line in lines:
            if line.startswith('DA:'):
                found += 1
                hit += int(line.split(',')[1]) > 0
    if not found:
        raise ValueError('LCOV report contains no frontend production lines')
    ratio = hit / found
    if ratio < 0.8:
        raise ValueError(f'Frontend coverage below 80%: {ratio:.1%}')
    return ratio


if __name__ == '__main__':
    print(f'Backend production coverage: {check_backend():.1%}')
    print(f'Frontend production coverage: {check_frontend():.1%}')
