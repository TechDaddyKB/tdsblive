# TDSBLive

[![Windows CI](https://github.com/camarokris/tdsblive/actions/workflows/windows-ci.yml/badge.svg)](https://github.com/camarokris/tdsblive/actions/workflows/windows-ci.yml)

A local-first Streamer.bot companion for Rumble events, combined chat, visual
overlays, supporter tracking and automation. MIT licensed.

**Local HTTP is supported. HTTPS and certificates are not required.** The host
defaults to `http://127.0.0.1:17474`. Optional authenticated LAN operation is a
planned capability; it is not enabled in the initial scaffold.

This repository currently contains the qualified Rumble evidence and the G01
development scaffold: a minimal HTTP status endpoint, editor/runtime build
targets, shared polling constraints, unit/integration tests and Windows CI.
It is **not yet a usable streaming companion release**. Integration, persistence,
the real editor, widgets and installation are tracked by stable goals in the
[implementation plan](docs/implementation-plan.md).

## Development

Use the exact SDK in `global.json`, Node in `.nvmrc`, npm in `package.json`, and
Python 3.14.7. The SonarQube CLI 1.9.0 is required by evidence tooling's mandatory
secrets-on-read safeguards. Windows CI installs its pinned binary automatically.

Scan files before reading them, as specified in [AGENTS.md](AGENTS.md). Then:

```bash
dotnet restore TDSBLive.slnx --locked-mode
npm ci
dotnet build TDSBLive.slnx --configuration Release --no-restore
dotnet test TDSBLive.slnx --configuration Release --no-build --settings coverage.runsettings --collect:"XPlat Code Coverage" --logger trx --results-directory TestResults
npm run lint
npm run typecheck
npm run build
npm run test:coverage
python tools/rumble_evidence.py verify
python -m unittest discover -s tests/replay -v
python tools/verify_coverage.py
```

Run the minimal host with `dotnet run --project src/ExtensionSuite.Host`; run the
separate editor scaffold with `npm run dev --workspace @tdsblive/editor`. Serving
the built editor from the host and persistent configuration belong to G02.

## Requirements and quality

- [Reviewed specification](SPEC.md) and [referenceable goals](docs/implementation-plan.md).
- [Rumble evidence analysis](docs/rumble-analysis.md), including unknown schemas,
  snapshot limits and sanitization boundaries.
- [Architecture](docs/architecture.md), [security](docs/security.md),
  [testing](docs/testing.md) and [contributing](CONTRIBUTING.md).
- [SonarQube Cloud project](https://sonarcloud.io/summary/new_code?id=camarokris_tdsblive).

Original archives, credentials, private captures, user data, generated reports
and build output are not committed. Public fixtures retain specified date/amount
metadata but replace direct private strings; they are not a claim of complete
anonymization. Subscriber/gift behavior remains unverified against live captures.
