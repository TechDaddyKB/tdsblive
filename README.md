# TDSBLive

[![Windows CI](https://github.com/techdaddykb/tdsblive/actions/workflows/windows-ci.yml/badge.svg)](https://github.com/techdaddykb/tdsblive/actions/workflows/windows-ci.yml)

A local-first Streamer.bot companion for Rumble events, combined chat, visual
overlays, supporter tracking and automation. MIT licensed.

**Local HTTP is supported. HTTPS and certificates are not required.** The host
defaults to `http://127.0.0.1:17474`. Optional authenticated LAN operation is a
foundation capability; it is disabled by default and requires Windows DPAPI admin setup.

Goals G00–G09 are delivered: Streamer.bot/Speaker.bot connections, reliable Rumble
ingestion, combined chat, visual editing and alerts, supporter accounting, donor
widgets and automation. G10 is preparing the Windows ZIP/installer, first-run
setup, recovery and illustrated documentation. **A completed MVP release has not
yet been qualified.** Current evidence and limitations are recorded in the
[implementation plan](docs/implementation-plan.md). VTube Studio work is on hold;
use Streamer.bot's built-in integration.

## User guide

The plain-language [repository guide](docs/user-guide/Home.md) and
[GitHub wiki](https://github.com/TechDaddyKB/tdsblive/wiki) are being completed with
G10. Start with [Chat and OBS](docs/user-guide/Chat-and-OBS.md) if you already have
the app running. The developer commands below are not the intended release
installation path.

## Development

Use the exact SDK in `global.json`, Node in `.nvmrc`, npm in `package.json`, and
Python 3.14.7. The SonarQube CLI 1.9.0 is required by evidence tooling's mandatory
secrets-on-read safeguards. Windows CI installs its pinned binary automatically.

Scan files before reading them, as specified in [AGENTS.md](AGENTS.md). Then:

```bash
dotnet restore TDSBLive.slnx --locked-mode
npm ci
npm run build
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

Run the foundation host with `dotnet run --project src/ExtensionSuite.Host` and open
`http://127.0.0.1:17474/editor`. Build assets before the host so they are copied to
its output. Use `npm run dev --workspace @tdsblive/editor` for frontend development.
See [foundation API](docs/foundation-api.md) for bootstrap configuration, DPAPI
credential setup, authenticated LAN and generated contracts.

## Requirements and quality

- [Reviewed specification](SPEC.md) and [referenceable goals](docs/implementation-plan.md).
- [Rumble evidence analysis](docs/rumble-analysis.md), including unknown schemas,
  snapshot limits and sanitization boundaries.
- [Architecture](docs/architecture.md), [security](docs/security.md),
  [development](docs/development.md), [testing](docs/testing.md) and
  [contributing](CONTRIBUTING.md).
- [Installation](docs/installation.md), [Rumble](docs/rumble.md),
  [events](docs/events.md), [overlays](docs/overlays.md), [widgets](docs/widgets.md),
  [automation](docs/automation.md) and [database/recovery](docs/database.md).
- [SonarQube Cloud project](https://sonarcloud.io/summary/new_code?id=camarokris_tdsblive).

Original archives, credentials, private captures, user data, generated reports
and build output are not committed. Public fixtures retain specified date/amount
metadata but replace direct private strings; they are not a claim of complete
anonymization. Subscriber/gift behavior remains unverified against live captures.
