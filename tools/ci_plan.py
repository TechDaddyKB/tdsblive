#!/usr/bin/env python3
"""Select additive PR qualification; protected-main and manual runs are always full."""
import argparse
import json
import os
from pathlib import Path, PurePosixPath
import re
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
FULL_PREFIXES = (
    '.github/', '.config/', 'packaging/', 'tools/', 'tests/replay/',
    'tests/fixtures/', 'tests/unit/ExtensionSuite.Desktop.Tests/',
    'src/ExtensionSuite.Desktop/', 'src/ExtensionSuite.DesktopControl/',
    'src/ExtensionSuite.Host/Properties/', 'docs/user-guide/', 'docs/licenses/',
)
HOST_LIFECYCLE = {
    'ApplicationLifecycle.cs', 'ApplicationRelauncher.cs', 'ConfigurationStore.cs',
    'EditorBrowserLauncher.cs', 'HostApplicationRunner.cs', 'Program.cs',
    'RecoveryArchive.cs', 'RecoveryEndpoints.cs', 'RecoveryRestore.cs', 'RecoveryShutdown.cs',
}
STANDARD_ROOT_DOCS = {'README.md', 'CONTRIBUTING.md'}
STANDARD_FRONTEND_CONFIG = {'eslint.config.mjs', 'tsconfig.json', 'vitest.config.ts'}
BUILD_SUFFIXES = ('.csproj', '.sln', '.slnx', '.props', '.targets', '.pubxml', '.runsettings')
SOURCE_SUFFIXES = {'.cs'}
FRONTEND_SUFFIXES = {'.ts', '.tsx', '.js', '.jsx', '.mjs', '.css', '.html', '.svg', '.png', '.woff2'}


def validate_path(filename: str) -> PurePosixPath:
    path = PurePosixPath(filename)
    if (not filename or path.is_absolute() or '..' in path.parts or '\\' in filename
            or ':' in filename or filename != path.as_posix()):
        raise ValueError('Changed paths must be canonical repository-relative paths')
    return path


def standard_path(filename: str, path: PurePosixPath) -> bool:
    return (
        filename in STANDARD_ROOT_DOCS or filename in STANDARD_FRONTEND_CONFIG
        or filename == 'docs/contracts/openapi.json'
        or filename.startswith('docs/') and path.suffix == '.md'
        or filename.startswith('frontend/') and path.suffix in FRONTEND_SUFFIXES
        or filename.startswith(('src/', 'tests/unit/', 'tests/integration/')) and path.suffix in SOURCE_SUFFIXES
    )


def full_reason(filename: str) -> str | None:
    path = validate_path(filename)
    if filename.startswith(FULL_PREFIXES):
        return 'Desktop, packaging, qualification tooling, CI or shipped guide changed'
    if (filename.endswith(BUILD_SUFFIXES) or path.name in ('package.json', 'package-lock.json')
            or path.name.startswith('packages.') and path.name.endswith('.lock.json')):
        return 'Dependency, build or coverage configuration changed'
    if filename.startswith('src/ExtensionSuite.Host/'):
        if path.name in HOST_LIFECYCLE or path.name.startswith('Desktop'):
            return 'Host ownership, startup or recovery changed'
    if filename.startswith('tests/') and any(
        word in path.name for word in ('Desktop', 'Lifecycle', 'Recovery', 'Relaunch')
    ):
        return 'Desktop or recovery regression tests changed'
    if standard_path(filename, path):
        return None
    return 'Unrecognized change requires full qualification'


def select_plan(event: str, files: list[str], force_full: bool = False) -> dict[str, str]:
    if event not in ('pull_request', 'push', 'workflow_dispatch'):
        raise ValueError('Unsupported CI event')
    reasons = {reason for filename in files if (reason := full_reason(filename))}
    if event != 'pull_request':
        reasons.add('Protected-main and manual validation always run full qualification')
    if force_full:
        reasons.add('Agent or maintainer requested full qualification')
    if not files and event == 'pull_request':
        reasons.add('No changed paths; run full rather than infer a reduced scope')
    full = bool(reasons)
    return {
        'full': str(full).lower(),
        'tier': 'full' if full else 'standard',
        'reason': '; '.join(sorted(reasons)) if reasons else 'Application or non-shipped documentation changes',
    }


def checked_sha(value: str) -> str:
    if not re.fullmatch(r'[0-9a-f]{40}', value):
        raise ValueError('Expected a full Git commit SHA')
    return value


def revision(root: Path, value: str) -> str:
    if value == 'HEAD':
        result = subprocess.check_output(['git', 'rev-parse', '--verify', 'HEAD^{commit}'], cwd=root)
    elif value == 'origin/main':
        result = subprocess.check_output(['git', 'rev-parse', '--verify', 'origin/main^{commit}'], cwd=root)
    else:
        return checked_sha(value)
    sha = result.decode().strip()
    return checked_sha(sha)


def changed_files(root: Path, base: str, head: str) -> list[str]:
    base, head = checked_sha(base), checked_sha(head)
    data = subprocess.check_output(
        ['git', 'diff', '--name-only', '--no-renames', '-z', base + '...' + head], cwd=root,
    )
    return [item.decode('utf-8') for item in data.split(b'\0') if item]


def full_requested(root: Path, base: str, head: str) -> bool:
    base, head = checked_sha(base), checked_sha(head)
    messages = subprocess.check_output(['git', 'log', '--format=%B%x00', base + '..' + head], cwd=root)
    subprocess.run(['sonar', 'analyze', 'secrets', '--stdin'], input=messages, check=True)
    return any(line.strip().lower() == 'ci: full' for line in messages.decode('utf-8').splitlines())


def emit(plan: dict[str, str], output: Path | None, summary: Path | None) -> None:
    if output:
        with output.open('a', encoding='utf-8') as stream:
            for key, value in plan.items():
                stream.write(f'{key}={value}\n')
    text = (
        f'## CI validation: {plan["tier"]}\n\n{plan["reason"]}.\n\n'
        'Build, lint, types, tests, coverage, Sonar and managed-host browser checks remain required. '
        'Full adds native Windows packages, Linux companion qualification and desktop diagnostics.\n'
    )
    if summary:
        with summary.open('a', encoding='utf-8') as stream:
            stream.write(text)
    print(text)


def linux_coverage_state(jobs: list[dict]) -> bool:
    matches = [job for job in jobs if job['name'] == 'Linux desktop tests and coverage']
    if len(matches) != 1:
        raise ValueError('Expected exactly one same-run Linux desktop coverage job')
    job = matches[0]
    if job['status'] != 'completed':
        return False
    if job['conclusion'] != 'success':
        raise ValueError('Linux desktop tests or coverage export failed; analysis cannot proceed')
    return True


def wait_for_linux_coverage(repository: str, run_id: str, timeout: int) -> None:
    if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_.-]*/[A-Za-z0-9][A-Za-z0-9_.-]*', repository):
        raise ValueError('Invalid repository name')
    if not re.fullmatch(r'[1-9]\d*', run_id) or timeout <= 0:
        raise ValueError('Require a positive run ID and bounded timeout')
    endpoint = f'repos/{repository}/actions/runs/{run_id}/jobs?filter=latest&per_page=100'
    deadline = time.monotonic() + timeout
    while True:
        result = subprocess.run(['gh', 'api', endpoint], capture_output=True, text=True, timeout=30)
        if result.returncode:
            raise RuntimeError('Cannot verify same-run Linux coverage job; GitHub response omitted')
        if linux_coverage_state(json.loads(result.stdout)['jobs']):
            print('Same-run Linux desktop job succeeded; coverage may now be downloaded and verified.')
            return
        if time.monotonic() >= deadline:
            raise TimeoutError('Timed out waiting for successful same-run Linux desktop coverage')
        time.sleep(min(5, max(0, deadline - time.monotonic())))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    select = commands.add_parser('select')
    select.add_argument('--event', choices=('pull_request', 'push', 'workflow_dispatch'), required=True)
    select.add_argument('--base')
    select.add_argument('--head', default='HEAD')
    select.add_argument('--full', action='store_true')
    wait = commands.add_parser('wait-coverage')
    wait.add_argument('--repository', default=os.environ.get('GITHUB_REPOSITORY', ''))
    wait.add_argument('--run-id', default=os.environ.get('GITHUB_RUN_ID', ''))
    wait.add_argument('--timeout', type=int, default=900)
    args = parser.parse_args()
    if args.command == 'wait-coverage':
        wait_for_linux_coverage(args.repository, args.run_id, args.timeout)
        return
    files: list[str] = []
    force_full = args.full
    if args.event == 'pull_request':
        if not args.base:
            parser.error('PR selection requires an explicit base revision')
        base, head = revision(ROOT, args.base), revision(ROOT, args.head)
        files = changed_files(ROOT, base, head)
        force_full = force_full or full_requested(ROOT, base, head)
    output = os.environ.get('GITHUB_OUTPUT')
    summary = os.environ.get('GITHUB_STEP_SUMMARY')
    emit(select_plan(args.event, files, force_full),
         Path(output) if output else None, Path(summary) if summary else None)


if __name__ == '__main__':
    main()
