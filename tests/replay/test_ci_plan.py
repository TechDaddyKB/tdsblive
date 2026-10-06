"""Additive CI scope, cumulative Git changes and fail-closed coverage admission."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import textwrap
import unittest
from unittest.mock import patch

from tools.ci_plan import (
    changed_files, emit, full_requested, linux_coverage_state, revision,
    select_plan, wait_for_linux_coverage,
)


class CiPlanTests(unittest.TestCase):
    def test_standard_keeps_all_baseline_checks_for_application_and_non_shipped_docs(self):
        for filename in (
            'frontend/editor/src/App.tsx', 'frontend/overlay-runtime/src/main.tsx',
            'src/ExtensionSuite.Core/Events.cs', 'src/ExtensionSuite.Host/FinancialEndpoints.cs',
            'tests/unit/ExtensionSuite.Core.Tests/EventsTests.cs',
            'tests/integration/ExtensionSuite.Host.Tests/FinancialTests.cs',
            'docs/implementation-plan.md', 'README.md', 'CONTRIBUTING.md',
            'docs/contracts/openapi.json', 'vitest.config.ts',
        ):
            with self.subTest(filename=filename):
                self.assertEqual('false', select_plan('pull_request', [filename])['full'])

    def test_sensitive_and_unknown_changes_force_full_even_mixed_with_standard(self):
        for filename in (
            '.github/workflows/windows-ci.yml', '.github/dependabot.yml',
            '.config/dotnet-tools.json', '.nvmrc', 'global.json', 'AGENTS.md', 'LICENSE',
            'Directory.Build.props', 'Directory.Packages.props', 'coverage.runsettings',
            'package.json', 'package-lock.json', 'tools/browser-qualification/package-lock.json',
            'src/ExtensionSuite.Core/packages.lock.json', 'src/ExtensionSuite.Core/New.csproj',
            'src/ExtensionSuite.Host/Properties/PublishProfiles/WindowsPortable.pubxml',
            'src/ExtensionSuite.Host/ApplicationLifecycle.cs',
            'src/ExtensionSuite.Host/DesktopSession.cs', 'src/ExtensionSuite.Host/Program.cs',
            'src/ExtensionSuite.Host/RecoveryRestore.cs',
            'src/ExtensionSuite.Desktop/LinuxSetupWindow.cs',
            'src/ExtensionSuite.DesktopControl/DesktopProtocol.cs',
            'tests/unit/ExtensionSuite.Desktop.Tests/LinuxStartupTests.cs',
            'tests/integration/ExtensionSuite.Host.Tests/RecoveryTests.cs',
            'tests/fixtures/new.json', 'tests/replay/test_ci_plan.py',
            'tools/ci_plan.py', 'tools/qualify_windows_tray.ps1',
            'docs/user-guide/Home.md', 'docs/user-guide/images/tray.png',
            'docs/licenses/new.md', 'packaging/windows/tdsblive.iss', 'new-area/new.file',
        ):
            with self.subTest(filename=filename):
                self.assertEqual('true', select_plan('pull_request', ['README.md', filename])['full'])

    def test_main_manual_empty_diff_and_override_cannot_reduce_scope(self):
        for event, files, force in (
            ('push', ['README.md'], False), ('workflow_dispatch', ['README.md'], False),
            ('pull_request', [], False), ('pull_request', ['README.md'], True),
            ('pull_request', ['packaging/new.txt'], False),
        ):
            with self.subTest(event=event, files=files, force=force):
                self.assertEqual('full', select_plan(event, files, force)['tier'])
        with self.assertRaises(ValueError):
            select_plan('pull_request_target', ['README.md'])

    def test_noncanonical_paths_are_rejected_not_silently_skipped(self):
        for filename in ('', '../README.md', '/README.md', 'docs/../README.md',
                         'docs\\Home.md', 'C:README.md', './README.md', 'docs//Home.md'):
            with self.subTest(filename=filename), self.assertRaises(ValueError):
                select_plan('pull_request', [filename])

    def test_outputs_are_exact_lowercase_booleans_and_explain_selected_tier(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            plan = select_plan('pull_request', ['README.md'])
            emit(plan, root / 'output', root / 'summary')
            self.assertEqual(
                'full=false\ntier=standard\nreason=Application or non-shipped documentation changes\n',
                (root / 'output').read_text(),
            )
            self.assertIn('Build, lint, types, tests, coverage, Sonar', (root / 'summary').read_text())

    def test_cumulative_diff_preserves_deletions_and_both_sides_of_renames(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            def git(*args):
                return subprocess.check_output(['git', *args], cwd=root).decode().strip()
            git('init', '-q', '-b', 'main')
            git('config', 'user.name', 'CI fixture')
            git('config', 'user.email', 'ci-fixture@example.invalid')
            (root / 'packaging').mkdir()
            (root / 'packaging/old.cs').write_text('owned fixture')
            git('add', '.')
            git('commit', '-qm', 'Owned base')
            base = revision(root, 'HEAD')
            (root / 'frontend').mkdir()
            (root / 'packaging/old.cs').rename(root / 'frontend/new.ts')
            git('add', '-A')
            git('commit', '-qm', 'Move package input\n\nCI: full')
            (root / 'README.md').write_text('owned documentation')
            git('add', '.')
            git('commit', '-qm', 'Docs follow-up\n\nCI: skip')
            head = revision(root, 'HEAD')
            files = changed_files(root, base, head)
            self.assertEqual({'packaging/old.cs', 'frontend/new.ts', 'README.md'}, set(files))
            self.assertEqual('true', select_plan('pull_request', files)['full'])
            messages = subprocess.check_output(['git', 'log', '--format=%B%x00', base + '..' + head], cwd=root)
            with patch('tools.ci_plan.subprocess.check_output', return_value=messages), \
                    patch('tools.ci_plan.subprocess.run') as scan:
                self.assertTrue(full_requested(root, base, head))
                self.assertEqual(['sonar', 'analyze', 'secrets', '--stdin'], scan.call_args.args[0])
                self.assertTrue(scan.call_args.kwargs['check'])

    def test_full_trailer_is_scanned_and_only_increases_scope(self):
        for message, expected in (
            (b'Change\n\nCI: full\n\0', True), (b'Change\n\nci: FULL\n\0', True),
            (b'CI: standard\n\0', False), (b'CI: skip\n\0', False),
            (b'Mention CI: full in prose\n\0', False),
        ):
            with self.subTest(message=message), patch('tools.ci_plan.subprocess.check_output', return_value=message), \
                    patch('tools.ci_plan.subprocess.run') as scan:
                self.assertEqual(expected, full_requested(Path('.'), 'a' * 40, 'b' * 40))
                self.assertEqual(message, scan.call_args.kwargs['input'])
        with patch('tools.ci_plan.subprocess.check_output', return_value=b'CI: full'), \
                patch('tools.ci_plan.subprocess.run', side_effect=subprocess.CalledProcessError(1, 'sonar')):
            with self.assertRaises(subprocess.CalledProcessError):
                full_requested(Path('.'), 'a' * 40, 'b' * 40)

    def test_cli_revisions_cannot_be_options_ranges_or_unapproved_refs(self):
        for value in ('--help', '--output=private', 'HEAD~1', 'main', 'a' * 40 + '..HEAD'):
            for operation in (lambda: revision(Path('.'), value),
                              lambda: changed_files(Path('.'), value, 'a' * 40),
                              lambda: full_requested(Path('.'), 'a' * 40, value)):
                with self.subTest(value=value), patch('tools.ci_plan.subprocess.check_output') as git:
                    with self.assertRaises(ValueError):
                        operation()
                    git.assert_not_called()

    def test_named_refs_use_literal_commands_and_shas_do_not_become_git_arguments(self):
        sha = 'a' * 40
        with patch('tools.ci_plan.subprocess.check_output') as git:
            self.assertEqual(sha, revision(Path('.'), sha))
            git.assert_not_called()
        for value in ('HEAD', 'origin/main'):
            with self.subTest(value=value), patch('tools.ci_plan.subprocess.check_output', return_value=sha.encode()) as git:
                self.assertEqual(sha, revision(Path('.'), value))
                self.assertEqual(['git', 'rev-parse', '--verify', value + '^{commit}'], git.call_args.args[0])

    def test_linux_coverage_requires_exact_successful_job_not_just_an_artifact(self):
        success = {'name': 'Linux desktop tests and coverage', 'status': 'completed', 'conclusion': 'success'}
        self.assertTrue(linux_coverage_state([success]))
        self.assertFalse(linux_coverage_state([success | {'status': 'in_progress', 'conclusion': None}]))
        for jobs in ([], [success, success], [success | {'conclusion': 'failure'}],
                     [success | {'conclusion': 'cancelled'}], [success | {'conclusion': 'skipped'}]):
            with self.subTest(jobs=jobs), self.assertRaises(ValueError):
                linux_coverage_state(jobs)

    def test_wait_is_bounded_and_api_errors_never_admit_missing_coverage(self):
        job = {'name': 'Linux desktop tests and coverage', 'status': 'completed', 'conclusion': 'success'}
        response = subprocess.CompletedProcess([], 0, json.dumps({'jobs': [job]}), '')
        with patch('tools.ci_plan.subprocess.run', return_value=response) as api:
            wait_for_linux_coverage('TechDaddyKB/tdsblive', '123', 30)
            self.assertIn('actions/runs/123/jobs?filter=latest', api.call_args.args[0][-1])
            self.assertEqual(30, api.call_args.kwargs['timeout'])
        with patch('tools.ci_plan.subprocess.run', return_value=response.__class__([], 1, '', 'private error')):
            with self.assertRaisesRegex(RuntimeError, 'response omitted'):
                wait_for_linux_coverage('TechDaddyKB/tdsblive', '123', 30)
        pending = job | {'status': 'queued', 'conclusion': None}
        with patch('tools.ci_plan.subprocess.run', return_value=response.__class__([], 0, json.dumps({'jobs': [pending]}), '')), \
                patch('tools.ci_plan.time.monotonic', side_effect=[0, 31]):
            with self.assertRaises(TimeoutError):
                wait_for_linux_coverage('TechDaddyKB/tdsblive', '123', 30)
        for repository, run_id, timeout in (('../other', '123', 30), ('owner/repo', '0', 30),
                                            ('owner/repo', '123', 0)):
            with self.assertRaises(ValueError):
                wait_for_linux_coverage(repository, run_id, timeout)


class WorkflowWiringTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        path = Path(__file__).resolve().parents[2] / '.github/workflows/windows-ci.yml'
        subprocess.run(['sonar', 'analyze', 'secrets', str(path)], check=True)
        cls.workflow = path.read_text(encoding='utf-8')
        cls.gate = textwrap.dedent(cls.workflow.rsplit('        run: |\n', 1)[1])
        cls.bash = 'bash'
        if os.name == 'nt':
            git = shutil.which('git')
            if not git:
                raise FileNotFoundError('Git for Windows is required for workflow-script tests')
            bash = next((parent / 'bin/bash.exe' for parent in Path(git).parents
                         if (parent / 'bin/bash.exe').is_file()), None)
            if bash is None:
                raise FileNotFoundError('Git for Windows Bash is required; do not use the WSL launcher')
            cls.bash = str(bash)

    def gate_result(self, full, **changes):
        with tempfile.TemporaryDirectory() as directory:
            expected = 'success' if full == 'true' else 'skipped'
            env = os.environ | {
                'FULL': full, 'PLAN_RESULT': 'success', 'WINDOWS_RESULT': 'success',
                'LINUX_RESULT': 'success', 'PACKAGE_RESULT': expected, 'PROBE_RESULT': expected,
                'INVENTORY_RESULT': expected, 'GITHUB_STEP_SUMMARY': (Path(directory) / 'summary').as_posix(),
            } | changes
            return subprocess.run([self.bash, '-e', '-c', self.gate], env=env, capture_output=True, text=True)

    def test_actual_required_check_script_admits_only_selected_successes(self):
        for full in ('true', 'false'):
            with self.subTest(full=full):
                self.assertEqual(0, self.gate_result(full).returncode)
            for key in ('PLAN_RESULT', 'WINDOWS_RESULT', 'LINUX_RESULT'):
                for state in ('failure', 'skipped', 'cancelled'):
                    with self.subTest(full=full, key=key, state=state):
                        result = self.gate_result(full, **{key: state})
                        self.assertNotEqual(0, result.returncode)
                        self.assertIn('must succeed', result.stderr)
        for key in ('PACKAGE_RESULT', 'PROBE_RESULT', 'INVENTORY_RESULT'):
            for state in ('failure', 'skipped', 'cancelled'):
                with self.subTest(key=key, state=state):
                    self.assertNotEqual(0, self.gate_result('true', **{key: state}).returncode)
            self.assertNotEqual(0, self.gate_result('false', **{key: 'success'}).returncode)
        for full in ('', 'invalid'):
            self.assertNotEqual(0, self.gate_result(full).returncode)

    def test_routing_is_protected_parallel_and_not_a_workflow_path_skip(self):
        self.assertIn('ref: ${{ github.event.pull_request.base.sha || github.sha }}', self.workflow)
        self.assertIn("'full=true' 'tier=full'", self.workflow)
        selection = self.workflow.split('- name: Select additive checks from the cumulative PR diff', 1)[1].split('  linux-desktop:', 1)[0]
        self.assertIn("SONARQUBE_CLI_TOKEN: ${{ env.RUN_SONAR == 'true' && secrets.SONAR_TOKEN || '' }}", selection)
        self.assertNotIn('pull_request_target:', self.workflow)
        self.assertNotIn('paths-ignore:', self.workflow)
        windows = self.workflow.split('  windows:\n', 1)[1].split('  linux-package:\n', 1)[0]
        self.assertIn('needs: validation-plan', windows)
        self.assertNotIn('needs: linux-desktop', windows)
        self.assertLess(windows.index('wait-coverage'), windows.index('Download successful Linux coverage'))
        self.assertIn('python tools/linux_coverage.py import', windows)
        for name in ('Build self-contained Windows ZIP and per-user installer',
                     'Qualify native Windows packages', 'Qualify packaged guide without network access',
                     'Upload candidate Windows packages'):
            self.assertIn(f'- name: {name}\n        if: needs.validation-plan.outputs.full == \'true\'', windows)
        self.assertIn('name: Windows build and tests\n    needs:', self.workflow)
        self.assertIn('needs: [validation-plan, linux-desktop, windows, linux-package, native-tray-probe, desktop-inventory]\n    if: always()', self.workflow)
        self.assertIn("if: env.RUN_SONAR == 'true'", windows)

    def test_browser_cache_is_versioned_and_prs_cannot_save_it(self):
        self.assertIn("key: windows-playwright-${{ hashFiles('tools/browser-qualification/package-lock.json') }}", self.workflow)
        self.assertIn("if: github.ref == 'refs/heads/main' && steps.playwright-cache.outputs.cache-hit != 'true'", self.workflow)
        self.assertIn('tools/api-type-generator/package-lock.json', self.workflow)
        self.assertIn('**/packages.win-x64.lock.json', self.workflow)


if __name__ == '__main__':
    unittest.main()
