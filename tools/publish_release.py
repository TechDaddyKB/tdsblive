"""Publish verified same-commit packages; never replace conflicting public history."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import tarfile
import tempfile
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

REPOSITORY = 'TechDaddyKB/tdsblive'
ROOT = Path(__file__).resolve().parents[1]


def scan(*paths):
    subprocess.run(['sonar', 'analyze', 'secrets', '--', *map(str, paths)], check=True)


def api(endpoint, method='GET', payload=None, missing=False):
    args = ['gh', 'api', f'repos/{REPOSITORY}/{endpoint}', '--method', method]
    if payload is not None:
        args += ['--input', '-']
    result = subprocess.run(args, input=json.dumps(payload) if payload is not None else None,
                            capture_output=True, text=True)
    if result.returncode:
        if missing and 'HTTP 404' in result.stderr:
            return None
        raise RuntimeError(f'GitHub API failed for {endpoint}; no credentials or response body logged')
    return json.loads(result.stdout) if result.stdout.strip() else None


def qualified_run(run_id, commit):
    if not re.fullmatch(r'[1-9][0-9]*', run_id) or not re.fullmatch(r'[0-9a-f]{40}', commit):
        raise ValueError('Require an exact run ID and full source SHA')
    run = api(f'actions/runs/{run_id}')
    if (run['name'] != 'Windows CI' or run['head_sha'] != commit or run['head_branch'] != 'main'
            or run['event'] not in ('push', 'workflow_dispatch') or run['status'] != 'completed'
            or run['conclusion'] != 'success' or run['head_repository']['full_name'].lower() != REPOSITORY.lower()):
        raise ValueError('Release requires a successful same-commit protected-main Windows CI run')
    checks = api(f'commits/{commit}/check-runs?per_page=100')['check_runs']
    # The API returns the latest run of each check by default. Include package and
    # security checks, not only the branch protection minimum.
    required = {'Windows build and tests', 'Linux desktop tests and coverage', 'Linux companion package',
                'Windows native tray probe', 'SonarCloud Code Analysis',
                'Analyze (csharp)', 'Analyze (python)', 'Analyze (javascript-typescript)'}
    successful = {item['name'] for item in checks if item['status'] == 'completed' and item['conclusion'] == 'success'}
    if not required <= successful:
        raise ValueError('Current source lacks successful required build, package or security checks: ' +
                         ', '.join(sorted(required-successful)))


def version():
    path = ROOT / 'Directory.Build.props'
    scan(path)
    value = ET.fromstring(path.read_text()).findtext('.//Version')
    if not value or not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+', value):
        raise ValueError('Invalid central release version')
    return value


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def checksum_entries(path):
    scan(path)
    result = {}
    for line in path.read_text().splitlines():
        match = re.fullmatch(r'([0-9a-f]{64})  (TDSBLive-[0-9.]+-(?:win-x64(?:-setup\.exe|\.zip)|linux-x64-wine\.tar\.gz))', line)
        if not match or match[2] in result:
            raise ValueError('Invalid or duplicate checksum entry')
        result[match[2]] = match[1]
    return result


def marker(data):
    if len(data) > 65536:
        raise ValueError('Oversized package marker')
    with tempfile.TemporaryDirectory(prefix='tdsblive-release-marker-') as directory:
        path = Path(directory) / 'TDSBLive.package.json'
        path.write_bytes(data)
        scan(path)  # Compressed evidence is scanned after decompression.
        return json.loads(path.read_text())


def verify_sources(sources, sums):
    for name, path in sources.items():
        if not path.is_file() or path.is_symlink():
            raise ValueError('Package assets must be ordinary files')
        scan(path)
        if digest(path) != sums[name]:
            raise ValueError('Package checksum mismatch: '+name)


def verify_markers(sources, sums, release_version, commit):
    win_zip = f'TDSBLive-{release_version}-win-x64.zip'
    linux_tar = f'TDSBLive-{release_version}-linux-x64-wine.tar.gz'
    expected = {'formatVersion': 1, 'version': release_version, 'sourceCommit': commit, 'target': 'win-x64'}
    with zipfile.ZipFile(sources[win_zip]) as archive:
        entries = [entry for entry in archive.infolist() if entry.filename == 'TDSBLive.package.json']
        if len(entries) != 1 or entries[0].file_size > 65536 or marker(archive.read(entries[0])) != expected:
            raise ValueError('Windows archive is not the exact release source')
    top = f'TDSBLive-{release_version}-linux-x64-wine/'
    with tarfile.open(sources[linux_tar], 'r:gz') as archive:
        for name, expected_marker in {
            top+'TDSBLive.package.json': {'formatVersion': 1, 'version': release_version, 'sourceCommit': commit,
                'target': 'linux-x64-wine', 'backendArchive': win_zip, 'backendSha256': sums[win_zip]},
            top+'backend/TDSBLive.package.json': expected,
        }.items():
            entries = [entry for entry in archive.getmembers() if entry.name == name]
            if len(entries) != 1 or not entries[0].isfile() or entries[0].size > 65536:
                raise ValueError('Missing or invalid Linux/backend source marker')
            if marker(archive.extractfile(entries[0]).read()) != expected_marker:
                raise ValueError('Linux bundle is not the exact release source and Windows archive')


def packages(windows, linux, destination, release_version, commit):
    win_zip = f'TDSBLive-{release_version}-win-x64.zip'
    linux_tar = f'TDSBLive-{release_version}-linux-x64-wine.tar.gz'
    win_names = {win_zip, f'TDSBLive-{release_version}-win-x64-setup.exe'}
    win_sums, linux_sums = checksum_entries(windows/'SHA256SUMS.txt'), checksum_entries(linux/'SHA256SUMS.txt')
    if set(win_sums) != win_names or set(linux_sums) != {linux_tar}:
        raise ValueError('Require exactly the two Windows and one Linux application assets')
    sources = {name: windows/name for name in win_names} | {linux_tar: linux/linux_tar}
    sums = win_sums | linux_sums
    verify_sources(sources, sums)
    verify_markers(sources, sums, release_version, commit)
    if destination.exists():
        raise ValueError('Choose a fresh publication staging directory')
    destination.mkdir(parents=True)
    for name, source in sources.items():
        shutil.copyfile(source, destination/name)
    (destination/'SHA256SUMS.txt').write_text(''.join(f'{sums[name]}  {name}\n' for name in sorted(sums)))
    scan(destination)
    return {name: digest(destination/name) for name in [*sources, 'SHA256SUMS.txt']}


def download_digest(url):
    if not url.startswith('https://github.com/'+REPOSITORY+'/releases/download/'):
        raise ValueError('Unexpected public asset URL')
    request = urllib.request.Request(url, headers={'User-Agent': 'TDSBLive-release-verifier'})
    value = hashlib.sha256()
    with urllib.request.urlopen(request, timeout=120) as response:
        if not response.url.startswith('https://'):
            raise ValueError('Public download redirected to insecure transport')
        while block := response.read(1024*1024):
            value.update(block)
    return value.hexdigest()


def ensure_tag(release_version, commit):
    tag = 'v'+release_version
    ref = api('git/ref/tags/'+tag, missing=True)
    if ref is None:
        api('git/refs', 'POST', {'ref':'refs/tags/'+tag, 'sha':commit})
    elif ref['object']['type'] != 'commit' or ref['object']['sha'] != commit:
        raise ValueError('Existing release tag conflicts; never move or replace it')
    return tag


def verify_uploaded_assets(assets, sums):
    for name, asset in assets.items():
        with tempfile.TemporaryFile() as stream:
            result = subprocess.run(['gh','api',f'repos/{REPOSITORY}/releases/assets/{asset["id"]}',
                '-H','Accept: application/octet-stream'],stdout=stream,stderr=subprocess.PIPE)
            if result.returncode:
                raise RuntimeError('Cannot verify existing release asset')
            stream.seek(0)
            if hashlib.file_digest(stream,'sha256').hexdigest() != sums[name]:
                raise ValueError('Existing asset conflicts; never overwrite: '+name)


def publish(directory, sums, release_version, commit, notes):
    tag = ensure_tag(release_version, commit)
    release = api('releases/tags/'+tag, missing=True)
    if release is None:
        scan(notes)
        release = api('releases', 'POST', {'tag_name':tag, 'target_commitish':commit,
            'name':'TDSBLive '+release_version, 'body':notes.read_text(), 'draft':True, 'prerelease':False})
    if release['prerelease'] or release['tag_name'] != tag:
        raise ValueError('Existing release metadata conflicts')
    assets = {asset['name']: asset for asset in release['assets']}
    if len(assets) != len(release['assets']) or not set(assets) <= set(sums):
        raise ValueError('Conflicting or duplicate release assets')
    verify_uploaded_assets(assets, sums)
    missing = sorted(set(sums)-set(assets))
    if missing:
        if not release['draft']:
            raise ValueError('Published release is incomplete; do not silently add missing assets')
        subprocess.run(['gh','release','upload',tag,*[str(directory/name) for name in missing],
                        '--repo',REPOSITORY],check=True)
    # Re-download every asset from the draft before publishing. On a retry this
    # verifies the existing assets rather than overwriting them.
    verified = api(f'releases/{release["id"]}')
    if {asset['name'] for asset in verified['assets']} != set(sums):
        raise ValueError('Release asset inventory mismatch')
    verify_uploaded_assets({asset['name']:asset for asset in verified['assets']}, sums)
    published = api(f'releases/{release["id"]}', 'PATCH', {'draft':False,'prerelease':False,'make_latest':'true'})
    latest = api('releases/latest')
    if latest['id'] != published['id'] or latest['draft'] or latest['prerelease']:
        raise ValueError('Release is not public Latest')
    for asset in latest['assets']:
        if download_digest(asset['browser_download_url']) != sums[asset['name']]:
            raise ValueError('Fresh unauthenticated public download checksum mismatch')
    print('Published and anonymously verified four release assets: '+published['html_url'])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--run-id', required=True)
    parser.add_argument('--commit', required=True)
    parser.add_argument('--validate-only', action='store_true')
    parser.add_argument('--windows', type=Path)
    parser.add_argument('--linux', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    qualified_run(args.run_id, args.commit)
    if not args.validate_only:
        if not all((args.windows,args.linux,args.output)): parser.error('Require package input and fresh output directories')
        release_version = version()
        sums = packages(args.windows,args.linux,args.output,release_version,args.commit)
        publish(args.output,sums,release_version,args.commit,ROOT/'docs/releases'/f'{release_version}.md')
