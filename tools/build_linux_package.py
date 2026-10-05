#!/usr/bin/env python3
"""Build a native Linux companion plus the same-commit Windows application."""
import argparse
import gzip
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import sys
import tarfile
import xml.etree.ElementTree as ET
import zipfile

from rumble_evidence import safe_read

ROOT = Path(__file__).resolve().parents[1]
MAXIMUM_ARCHIVE_BYTES = 2 * 1024 * 1024 * 1024
FORBIDDEN_FOLDERS = {'.git', '.secrets', 'credentials', 'references', 'node_modules', 'logs'}
FORBIDDEN_SUFFIXES = {'.db', '.sqlite', '.sqlite3', '.dpapi', '.log', '.pfx', '.pem'}


def scan(*paths):
    result = subprocess.run(['sonar', 'analyze', 'secrets', '--', *map(str, paths)],
                            capture_output=True, check=False)
    if result.returncode or b'No secrets found' not in result.stdout:
        raise ValueError('Secrets scan failed. Do not inspect, execute or publish the candidate.')


def archive_members(archive):
    entries = archive.infolist()
    if not entries or len(entries) > 10000 or sum(item.file_size for item in entries) > MAXIMUM_ARCHIVE_BYTES:
        raise ValueError('Windows archive is empty or too large')
    names = set()
    for entry in entries:
        name = entry.filename
        parts = PurePosixPath(name).parts
        folded = '/'.join(parts).casefold()
        if (not parts or name.startswith('/') or '\\' in name or '\0' in entry.orig_filename or ':' in name
                or '..' in parts or folded in names or entry.flag_bits & 1
                or stat.S_ISLNK(entry.external_attr >> 16)):
            raise ValueError('Unsafe or duplicate Windows archive member')
        names.add(folded)
        if (any(part.casefold() in FORBIDDEN_FOLDERS for part in parts)
                or parts[-1].casefold() == 'configuration.json'
                or PurePosixPath(name).suffix.lower() in FORBIDDEN_SUFFIXES):
            raise ValueError('Windows archive contains forbidden private/runtime content')
    return entries


def verify_checksum(archive, checksum_data):
    matches = []
    for line in checksum_data.decode('utf-8-sig').splitlines():
        values = line.split(maxsplit=1)
        if len(values) != 2 or not re.fullmatch(r'[0-9a-fA-F]{64}', values[0]):
            raise ValueError('Invalid checksum file')
        filename = values[1].strip()
        if '/' in filename or '\\' in filename:
            raise ValueError('Checksum entries must be plain filenames')
        if filename == archive.name:
            matches.append(values[0].lower())
    if len(matches) != 1:
        raise ValueError('Expected exactly one Windows archive checksum')
    with archive.open('rb') as stream:
        actual = hashlib.file_digest(stream, 'sha256').hexdigest()
    if matches[0] != actual:
        raise ValueError('Windows archive checksum mismatch')
    return actual


def extract_windows(archive_path, destination):
    if destination.exists():
        raise ValueError('Use a fresh backend extraction directory')
    with zipfile.ZipFile(archive_path) as archive:
        members = archive_members(archive)
        destination.mkdir(parents=True)
        for entry in members:
            target = destination.joinpath(*PurePosixPath(entry.filename).parts)
            if entry.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with archive.open(entry) as source, target.open('xb') as output:
                    shutil.copyfileobj(source, output)
    # Decompressed content is scanned before reading provenance or copying it.
    scan(destination)


def verify_backend(directory, version, commit):
    marker = json.loads(safe_read(directory / 'TDSBLive.package.json'))
    if (not isinstance(marker, dict) or type(marker.get('formatVersion')) is not int
            or marker != {'formatVersion': 1, 'version': version, 'sourceCommit': commit, 'target': 'win-x64'}):
        raise ValueError('Windows backend must be from this exact source commit and version')
    required = ('TDSBLive.exe', 'TDSBLive.dll', 'ExtensionSuite.DesktopControl.dll',
                'desktop/TDSBLive.Desktop.exe', 'wwwroot/editor/index.html', 'wwwroot/runtime/index.html',
                'guide/Home.html', 'LICENSE.txt', 'licenses/Inter-OFL.txt', 'licenses/Avalonia-MIT.txt')
    if any(not (directory / name).is_file() for name in required):
        raise ValueError('Windows archive is not the complete qualified application')


def validate_bundle(directory):
    for path in directory.rglob('*'):
        if path.is_symlink():
            raise ValueError('Linux package cannot contain filesystem links')
        parts = path.relative_to(directory).parts
        if (any(part.casefold() in FORBIDDEN_FOLDERS for part in parts)
                or path.name.casefold() == 'configuration.json'
                or path.suffix.lower() in FORBIDDEN_SUFFIXES):
            raise ValueError('Linux package contains forbidden private/runtime content')
    for name in ('TDSBLive', 'TDSBLive.Desktop', 'TDSBLive.Desktop.dll', 'TDSBLive.Desktop.runtimeconfig.json',
                 'tdsblive.svg', 'README.txt', 'TDSBLive.package.json', 'backend/TDSBLive.exe', 'guide/Home.html',
                 'LICENSE.txt', 'licenses/Inter-OFL.txt', 'licenses/Avalonia-MIT.txt'):
        if not (directory / name).is_file():
            raise ValueError('Linux package is incomplete')
    if not os.access(directory / 'TDSBLive', os.X_OK) or not os.access(directory / 'TDSBLive.Desktop', os.X_OK):
        raise ValueError('Native launchers must remain executable')


def write_archive(directory, destination, timestamp):
    with destination.open('xb') as output:
        with gzip.GzipFile(filename='', mode='wb', fileobj=output, mtime=timestamp) as compressed:
            with tarfile.open(fileobj=compressed, mode='w') as archive:
                for path in [directory, *sorted(directory.rglob('*'))]:
                    info = archive.gettarinfo(str(path), str(Path(directory.name) / path.relative_to(directory)))
                    info.uid = info.gid = 0
                    info.uname = info.gname = ''
                    info.mtime = timestamp
                    if info.isfile():
                        with path.open('rb') as source:
                            archive.addfile(info, source)
                    else:
                        archive.addfile(info)


def build(archive, checksums, output, dotnet):
    if sys.platform != 'linux':
        raise ValueError('Build Linux packages on Linux')
    version = ET.fromstring(safe_read(ROOT / 'Directory.Build.props')).findtext('.//Version')
    if not version or not re.fullmatch(r'\d+\.\d+\.\d+', version):
        raise ValueError('Invalid single-source release version')
    commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    if not re.fullmatch(r'[0-9a-f]{40}', commit):
        raise ValueError('Cannot identify package source commit')
    changes = subprocess.check_output(['git', 'status', '--porcelain', '--untracked-files=normal'], cwd=ROOT, text=True)
    if changes.strip():
        raise ValueError('Commit source changes before building a same-commit package')
    if archive.name != f'TDSBLive-{version}-win-x64.zip' or output.exists():
        raise ValueError('Use the current Windows archive and a fresh output directory')
    scan(archive, checksums)
    backend_hash = verify_checksum(archive, safe_read(checksums))
    bundle = output / f'TDSBLive-{version}-linux-x64-wine'
    extract_windows(archive, bundle / 'backend')
    verify_backend(bundle / 'backend', version, commit)
    project = ROOT / 'src/ExtensionSuite.Desktop/ExtensionSuite.Desktop.csproj'
    subprocess.run([dotnet, 'restore', str(project), '--runtime', 'linux-x64', '--locked-mode',
                    '-p:SelfContained=true', '-p:NuGetLockFilePath=packages.linux-x64.lock.json'], cwd=ROOT, check=True)
    subprocess.run([dotnet, 'publish', str(project), '--configuration', 'Release', '--no-restore',
                    '--runtime', 'linux-x64', '--self-contained', 'true', '-p:NuGetLockFilePath=packages.linux-x64.lock.json',
                    '-p:PublishSingleFile=false', '-p:PublishTrimmed=false', '-p:DebugType=none', '-p:DebugSymbols=false',
                    '--output', str(bundle)], cwd=ROOT, check=True)
    # Publish output is scanned before validation, copies or execution.
    scan(bundle)
    shutil.copy2(bundle / 'TDSBLive.Desktop', bundle / 'TDSBLive')
    shutil.copytree(bundle / 'backend/guide', bundle / 'guide')
    shutil.copy2(bundle / 'backend/LICENSE.txt', bundle / 'LICENSE.txt')
    shutil.copytree(bundle / 'backend/licenses', bundle / 'licenses')
    scan(ROOT / 'packaging/linux/README.txt')
    shutil.copy2(ROOT / 'packaging/linux/README.txt', bundle / 'README.txt')
    marker = {'formatVersion': 1, 'version': version, 'sourceCommit': commit, 'target': 'linux-x64-wine',
              'backendArchive': archive.name, 'backendSha256': backend_hash}
    (bundle / 'TDSBLive.package.json').write_text(json.dumps(marker, indent=2) + '\n', encoding='utf-8')
    scan(bundle)
    validate_bundle(bundle)
    timestamp = int(subprocess.check_output(['git', 'show', '-s', '--format=%ct', 'HEAD'], cwd=ROOT, text=True))
    result = output / f'TDSBLive-{version}-linux-x64-wine.tar.gz'
    write_archive(bundle, result, timestamp)
    with result.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    (output / 'SHA256SUMS.txt').write_text(f'{digest}  {result.name}\n', encoding='utf-8')
    scan(result, output / 'SHA256SUMS.txt')
    print(f'Built scanned same-commit Linux candidate: {result.name}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--windows-archive', type=Path, required=True)
    parser.add_argument('--checksums', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--dotnet', default='dotnet')
    args = parser.parse_args()
    build(args.windows_archive.resolve(), args.checksums.resolve(), args.output.resolve(), args.dotnet)
