# Testing and quality infrastructure

See README for exact commands. Windows CI is the authority for Windows builds;
local Linux checks are useful but do not substitute for the Windows run.

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
