# G01 validation record

Validated 2026-10-01. G00 prerequisites were checked against committed sanitized
fixtures, provenance metadata, all 785 polls and archive comparisons before setup.

## External evidence

- Public MIT repository: https://github.com/techdaddykb/tdsblive
- Passing Windows CI: https://github.com/techdaddykb/tdsblive/actions/runs/36836817528
- Analyzed commit: `fccfe4eaa052f5a1db23b7bc19c1804c30157477`
- GitHub-bound Sonar project: https://sonarcloud.io/dashboard?id=camarokris_tdsblive
- Sonar analysis: `f19bc92e-4fe7-4f86-bc18-8b48c2c1319b`

Windows CI restored locked NuGet/npm dependencies and tools, compiled all modules,
passed 12 backend and 9 frontend tests, lint/type checks and both Vite builds.
Public replay qualification passed 18 tests with two intentional archive-only
skips. Local qualification also compared the private source and ran the original
recorder's ten tests. No live automation or production ledger was involved.

Sonar API measures confirm 100% coverage, 19 lines to cover, zero uncovered lines,
12 imported backend tests and no errors/failures. File measures confirm OpenCover
coverage on Core's polling constraint and Host's HTTP entrypoint, plus LCOV
coverage on editor/runtime components and both React entrypoints. Sonar's line
count differs from raw coverage sequence-point counts. The quality gate is OK;
there are no security hotspots. Automatic analysis is disabled using the analysis
method activation API, then verified through settings.

## Protection and publication

Main requires `Windows build and tests` from GitHub Actions (app 15368) and
`SonarCloud Code Analysis` from Sonar (app 12526), with strict/up-to-date checks.
PRs, linear history and resolved conversations are required, including for
administrators. Force pushes and deletion are prohibited. No self-approval is
required for this personal repository; required checks remain mandatory.

GitHub secret scanning, push protection, vulnerability alerts and automated
Dependabot security fixes are enabled. Default Actions permissions are read-only;
PR review approval by Actions is disabled. Workflow Actions use immutable commits,
checkout does not persist credentials, and `pull_request_target` is absent.

Fork/Dependabot runs receive no Sonar token. Because deterministic Sonar scanning
requires authentication, these runs fail closed before source reads. A maintainer
must inspect and promote reviewed changes to a trusted branch for checks; skipping
analysis cannot satisfy the required Windows/Sonar checks. An actual Dependabot
PR run demonstrated the authentication failure without credential exposure.

Publication review scanned 109 candidate source files and decompressed fixtures.
Representative ignored paths include original references/ZIPs, secret files,
databases, captures, dependencies, generated coverage/logs and build artifacts.
Required sanitized fixtures, dependency locks and credential-free Sonar config
remain tracked. No embedded source-archive Git history is published.

## Limits and maintenance

Initial Sonar analysis reports 15 open findings, predominantly Python evidence-tool
complexity and scaffold/configuration maintainability. It also flagged evidence
reader path/argument handling. That reader now resolves paths inside the repository
and separates options from filenames; regression tests cover outside paths and
option-like filenames. These findings are not concealed by disabling rules. The
completion PR supplies reanalysis; remaining findings stay visible as quality debt.
A passing gate does not imply zero findings or qualification of future features.

The Actions token expires 2026-12-30 00:00 UTC; rotate it before then using account
security and update the Actions secret without printing/writing its value. See
[security](security.md) for details. Only test/coverage reports are uploaded, with
14-day retention; scanner state and references are excluded.

HTTPS is not required. The initial HTTP status endpoint test exercises ASP.NET's
test server without redirects or certificates. Real listener/editor/configuration,
secret storage and authenticated LAN behavior are G02/G10 work. All G02–G13
statuses remain Not started. No Streamer.bot, Speaker.bot, Rumble purchase, OBS
rendering/audio, financial ledger or packaging capability is claimed by G01.
