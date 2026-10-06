# Testing and quality infrastructure

See README for exact commands. Windows CI is the authority for Windows builds;
local Linux checks are useful but do not substitute for the Windows run.

## Change-aware PR validation

`Windows CI` runs on every PR update without workflow-level path exclusions.
`tools/ci_plan.py` selects **standard** or **full** from the cumulative base-to-head
diff, including deletions and both sides of renames. Selection runs the scanned
policy from the protected PR base, not editable PR routing code. If that base
does not yet contain the policy, CI explicitly requires full qualification for
the bootstrap run. Missing/invalid selection fails the required check.

| Scope | Required checks |
|---|---|
| Standard PR | Secrets scans; Linux desktop tests/coverage; Windows/frontend build, lint and types; all .NET/frontend/replay tests; production coverage thresholds; host HTTP/financial/desktop/recovery checks; managed-host browser suite; API type drift; Sonar analysis and quality gate |
| Full PR | Everything above plus Windows ZIP/installer build, portable/installed browser and native lifecycle qualification, offline packaged guide, Linux companion build/native UI qualification, native tray probe and desktop inventory |
| Main push or manual Windows CI | Always full, regardless of changed files |

Application C#/frontend changes and non-shipped Markdown documentation normally
use standard scope. Desktop, host ownership/startup/recovery, shipped user guide,
packaging, dependency manifests/lockfiles, build/coverage settings, CI, qualification
tooling and unrecognized files require full. This is an additive optimization:
even documentation-only PRs retain coverage and the required Sonar check.

Agents can escalate a PR with a standalone `CI: full` line in any PR commit message.
The request remains effective on subsequent commits while that commit remains in
PR history. No `CI: skip` or reduced tier is supported. Manual dispatch also provides
full validation; dispatch on a feature branch is not release-publication evidence.
After scanning the tool, preview committed changes locally with:

```bash
python tools/ci_plan.py select --event pull_request --base origin/main --head HEAD
python -m unittest tests.replay.test_ci_plan tests.replay.test_linux_coverage tests.replay.test_release_publication -v
```

Fetch the current main revision before previewing. Local previews use the local
policy, whereas Actions uses protected-base policy, so a policy-changing PR cannot
weaken its own selection. Selection never uses an AI model, credentials in commit
messages, a PR-authored file list or only the most recent commit.

Windows and Linux desktop validation start in parallel after policy selection.
Before importing coverage, Windows requires the Linux job in the same workflow run
to succeed; the existing import verifies the exact checkout commit and unchanged
measured counts. Missing coverage, API errors, failed/skipped/cancelled Linux tests
and bounded-wait expiry fail explicitly, rather than dropping Linux coverage.

NuGet cache keys include Windows runtime lockfiles; npm keys include the independent
browser/API-generator lockfiles. Playwright browsers are cached by their pinned
lockfile, with cache saves limited to main. PRs may restore the trusted main cache
but cannot populate it. Browser installation/qualification still runs on cache hits;
cache presence is not evidence that tests passed.

The final **Windows build and tests** check retains the protected branch's existing
name. It runs even when a dependency fails and requires both baseline jobs plus
every selected full-qualification job to succeed. Intentionally unselected package
jobs must be skipped only for standard scope. **SonarCloud Code Analysis** remains
a separate required check. Fork/Dependabot isolation is unchanged; missing secrets
still fail closed before source execution.

Standard PRs produce test evidence, not release candidates. Full protected-main
runs retain the same candidate artifact names consumed by `Publish release`,
which additionally requires the actual **Windows validation** job and all native,
Linux and security checks. Diagnostic packages never substitute for release assets.
Existing full qualification remains the authority for Windows/installer behavior;
local routing tests do not establish native execution or runtime savings.

## Initial meaningful tests

- Core xUnit tests enforce the seven-second Rumble default, inclusive normal
  bounds, the five-second safety floor and explicit advanced slower settings.
- Host integration tests use ASP.NET Core's test server to verify the status
  endpoint works over HTTP without a redirect/certificate requirement.
- React/Vitest tests verify accessible, truthful connection states and escaped
  integration text; the runtime composition test verifies transparency.
- G00 replay/tooling tests qualify all 785 sanitized polls, metadata/inference,
  source-to-fixture fidelity and 26 synthetic scenario inputs. Two archive-only
  tests intentionally skip in public CI. These are not production adapter tests.

## Reports and SonarQube

Use VSTest explicitly; Coverlet collectors are not compatible with MTP v2.
`coverage.runsettings` emits OpenCover and Cobertura for production assemblies;
TRX files provide test execution evidence. Frontend coverage uses LCOV.
`tools/verify_coverage.py` rejects absent/empty reports and requires at least 80%
backend production sequence-point and frontend production line coverage before
uploading analysis. SonarQube also enforces its new-code quality gate.

SonarScanner for .NET begins before compilation and ends after backend/frontend
tests. It imports `TestResults/**/coverage.opencover.xml`, `TestResults/**/*.trx`
and `coverage/lcov.info`. CI-based analysis is enabled; automatic analysis is
disabled. Test sources, fixtures, generated/dependency/build output and development
tooling/configuration are not production coverage targets. Handwritten runtime
entrypoints and domain/UI code remain included.

Actions upload TRX, coverage XML, LCOV and the isolated G02 editor screenshot,
retained for 14 days. The screenshot contains only generated non-production state.
They never upload scanner state or original references. The deterministic secrets
scanner requires a Sonar user token even for local scanning. Fork/Dependabot runs
receive no token and fail closed before reading source; they require maintainer
review and promotion to a trusted branch for tests and analysis. Protected merges
require the actual SonarQube check from that reviewed trusted branch.

G02 tests qualify canonical-event persistence/dedupe, migration rollback, outbox
recovery, isolated simulation, HTTP security, WebSockets, configuration and redacted
logs. Windows-only tests use actual DPAPI and authenticated non-loopback HTTP.
Separate CI child-process and fresh-browser qualifiers verify crash/restart,
OpenAPI drift and rendered editor/login shells without user or production data.

G03–G09 add bot protocol and trigger checks, Rumble replay/failure tests, rendered
chat/editor/alert qualification, financial precision and restart tests, donor
widgets and automation. Their goal evidence distinguishes simulated tests from
operator-confirmed OBS and audio output. Real Rumble subscription/gift behavior
remains gated where live schema evidence is missing.

G10 adds native Windows package qualification in
`tools/qualify_windows_package.ps1`: start the shipped self-contained EXE, load
browser pages, install, reinstall, uninstall and preserve separately stored data.
These checks must actually pass in Actions before packaging is called verified.
Native Windows streaming-PC performance is explicitly deferred by the operator;
Wine/Proton live/performance qualification and native Windows CI remain separate.

To regenerate the public chat guide screenshots, build frontend assets and the
Release host, then run `node tools/browser-qualification/guide-screenshots.mjs`
with the pinned `DOTNET_ROOT`. It creates an isolated temporary host and uses
made-up simulation messages only. Scan and visually review generated images
before staging. To prepare the wiki copy, run
`python tools/sync_user_guide.py /path/to/wiki-checkout`, review its diff, then
commit/push the wiki separately. The script scans source files and validates
local page/image links before copying; it does not publish automatically.

G11/G12 qualify advanced editor interactions, local Monaco, opaque custom worker
permissions, storage and malicious ZIP admission. G13 adds local compatibility
fixtures, rendered lifecycle/chat/store/status checks, value-free shape exports,
aggregate diagnostics and offline production-engine replay. The [requirements
matrix](requirements-matrix.md) records all numbered sections and explicit
live-evidence limits. Actual OBS sound/video, restart/restore and native Windows
packaging remain distinct gates. [Developer utilities](g13-compatibility.md)
operate on scanner-approved files and disposable local hosts.

## Tray-release qualification

The [1.0.1 plan](tray-release-plan.md) records G22–G24 and their current gates.
Local builds and headless desktop tests are preparation; actual native Windows
and Linux runs are required. Handwritten desktop production code remains in
coverage. Windows-only registration tests are explicitly skipped on Linux.

- `tools/qualify_desktop_control.py` uses an actual disposable host and bounded,
  authenticated control channel. It checks ownership, restart/restore, conflicts,
  retained state and SQLite integrity without live integrations.
- `tools/qualify_windows_tray.ps1` is guarded for an isolated interactive CI
  desktop and owned package. It checks native menus, normal browser handoff,
  Cancel-first keyboard behavior, Explorer loss/recovery, crash isolation,
  retained DPAPI credentials, manual/quiet launch, light/dark and actual display
  scaling. Never run its Explorer-changing scenarios on a streaming desktop.
- The full Windows job tests both portable and installed packages, their editor
  workflows and lifecycle, then reinstall/uninstall and opt-in startup behavior.
  The faster native probe provides diagnostic packages and screenshots; those
  artifacts do not replace the protected full job's release candidates.
- `tools/serve_tray_obs.py` prepares owned sample data for a separately created
  OBS Browser Source. Its `--self-check` is silent process evidence. Actual OBS
  rendering, reconnect, transparency and audio-signal observation are separate
  checks; recording, broadcasting and physical listening are not inferred.

The native Linux suite covers private profile ownership, alias/duplicate launches,
Wine/UMU argument and process boundaries, registration, dialogs and package
admission. Actual representative desktop checks and accepted resource/KDE limits
are recorded in the [tray plan](tray-release-plan.md). Keep unavailable evidence
explicit. Scan extracted packages and captures before inspection; publish reviewed
source and documentation, never private profiles or dependency/test output.

`Publish release` is a main-only manual workflow. Supply the successful **Windows
CI** run ID for the exact current main SHA. `tools/publish_release.py` requires
the full same-source Windows/Linux/security checks, validates archive markers
and checksums, creates a draft and immutable tag, verifies all uploaded assets,
publishes Latest and checks fresh unauthenticated downloads. Conflicting existing
tags/assets fail rather than being replaced. Release workflow tests reject
unqualified runs, mismatched source/backend hashes and conflicting assets.
