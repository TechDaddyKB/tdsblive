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

Actions upload only TRX, coverage XML and LCOV artifacts, retained for 14 days.
They never upload scanner state or original references. Fork/Dependabot builds
run the same tests but omit credential-bearing analysis; protected merges require
the actual SonarQube check from a reviewed trusted branch.

The current test corpus does not validate real Rumble purchases, production
dedupe, Streamer.bot actions, editor interaction, OBS audio, LAN authentication,
financial precision or installer behavior. Their owning goals add those tests and
record physical/live validation. Keep any unavailable evidence visibly blocked.
