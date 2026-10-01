# TDSBLive Implementation Plan

## Goal execution contract

This document is the self-contained implementation authority for TDSBLive. Goal IDs are stable: preserve them when revising requirements. The source requirements are `references/tdsblive_spec.md`; that local reference is not intended for public publication. This document records the approved implementation decisions so execution does not depend on chat history.

Invoke a goal with:

> Complete goal G02 from docs/implementation-plan.md, including any incomplete prerequisites.

For every goal, Codex must:

1. Scan workspace files with `sonar analyze secrets <path>` before reading them. If secrets are reported, do not read the file or continue the original task; explain the exposure risk and require credential rotation/removal.
2. Read the requested goal and referenced requirements; check prerequisites against repository, CI, and external-system evidence.
3. Complete missing prerequisites before dependent work. Do not expand into subsequent goals unless needed to satisfy the requested goal.
4. Implement deliverables, run the acceptance checks, and update status, validation evidence, and blockers here.
5. Never mark a goal complete merely because code compiles or mocked tests pass. Record missing real-world evidence explicitly.

Statuses: **Not started**, **In progress**, **Blocked**, **Complete**. A partially implemented goal with a missing acceptance gate remains In progress or Blocked. Record commit IDs, commands/results, CI links, integration versions, and dates as evidence without credentials or private data. These document statuses are separate from Codex goal-mode runtime status.

MVP completion requires G00–G10. Repository-wide completion requires G00–G13. Creating this document alone does not complete a goal.

## Locked architecture and defaults

| Item | Decision |
|---|---|
| Repository | Public `camarokris/tdsblive`, fresh history, default branch `main` |
| License | MIT |
| Branding | TDSBLive; configurable display branding |
| Backend | .NET 10 LTS, ASP.NET Core/Kestrel, EF Core SQLite, System.Text.Json, structured logging |
| Frontend | React, TypeScript, Vite, Zustand, React Moveable, Monaco |
| Distribution | Self-contained Windows x64 portable ZIP and Inno Setup installer |
| Host | `http://127.0.0.1:17474`, configurable port; clear conflict error |
| Streamer.bot | Configurable `ws://127.0.0.1:8080/` with authentication and reconnect |
| Speaker.bot | Configurable host/port, default 127.0.0.1:7680 |
| Application data | `%LOCALAPPDATA%\TDSBLive`, separate from installed binaries |
| SonarQube | SonarQube Cloud organization `camarokris`, project key `camarokris_tdsblive` |
| Currency conversion | Replaceable Frankfurter provider, cached historical rates, dated manual overrides |
| Time periods | OS timezone confirmed at setup; Monday-start weeks; UTC event storage |
| Rumble polling | 7 seconds, positive jitter up to 10%; normal UI 5–10 seconds, advanced slower only |
| Editor history | 750 ms autosave debounce, 50 revisions by default |

Streamer.bot remains the authority for existing platform integration and action execution. TDSBLive provides missing services: Rumble polling, normalization, persistence, aggregation, overlays, visual editing, supporter accounting, replay, and rules. Use isolated hosted services and adapters/strategies; an integration failure must not stop the host or other integrations. Design backend boundaries for future Linux hosting without claiming Linux release qualification.

**HTTP is supported and HTTPS is not required**, including optional authenticated LAN operation. Do not enable HTTPS redirects or require secure-context-only browser features for core workflows. Default binding is loopback; LAN binding is explicit opt-in with authentication. Document that LAN HTTP does not encrypt traffic. Optional HTTPS may be supported without becoming a release gate.

## Cross-goal contracts

### Events, persistence, APIs, and delivery

Use the canonical event envelope: UUIDv7 `id`; UTC `occurredAt`/`receivedAt`; `source`, `platform`, `type`, `nativeType`, optional `nativeId`; user login/display name/platform ID/avatar/badges; message text; optional monetary and stream data; `dedupeKey`; credential-redacted `raw`. Add live/simulation/replay provenance and correlation metadata to prevent bridge loops. Retain unknown source fields after credential redaction unless raw retention is disabled for privacy.

SQLite uses WAL, foreign keys, migrations, indexed aggregations, and transactional event/dedupe/checkpoint acceptance. Persist events/dedupe, polling/stream state, chat, supporters/identities, financial entries, FX/valuation rules, overlays/revisions/widgets/settings/state, assets, automation/execution records, configuration and logs. Use a durable outbox for downstream delivery. Financial ingestion is idempotent. External actions do not guarantee exactly-once execution: record uncertain outcomes and do not blindly retry ambiguous execution.

Serve `/editor`, `/overlay/{overlayId}`, `/assets/{assetId}`, `/api/...`, `/ws/editor`, and `/ws/overlay/{overlayId}`. REST covers status/integrations, events/chat, overlays/widgets, supporters/leaderboards/valuations, assets, rules, test events, backup/restore and import/export. Document with OpenAPI and generate frontend types. WebSocket operation envelopes include subscribe/event/ping and state updates, bounded delivery, reconnection/resubscription, and overlay-specific filtering. Raw payload access requires explicit widget permission.

Simulation/replay defaults to isolation: no live Streamer.bot actions, TTS, VTube Studio changes, or production ledger writes. Explicitly persisted test contributions are marked and excluded from ordinary totals by default.

### Security, repository hygiene, and quality

Use Windows DPAPI for stored integration credentials. Never send credentials or stream keys to widgets or ordinary logs. Redact before persistence/display/export; scanner success alone is not proof that credential-bearing data is publishable. Keep privileged endpoints same-origin, validate hosts and WebSocket origins, protect against CSRF, and never use wildcard privileged CORS. LAN access requires generated admin authentication and separate revocable, limited overlay tokens.

Ignore `references/` entirely, original archives, `.secrets/`, environment files, private certificates, local configuration overrides, capture/runtime databases and sidecars, logs, backups, diagnostic exports, user assets, build/cache output, dependencies, coverage/results, Playwright output, installer/release output, IDE state, Python caches and OS metadata. Deliberately track sample config, lockfiles, source assets and sanitized fixtures. Review staged files and verify ignore behavior before public push. Never import embedded recorder Git history.

Use Windows GitHub Actions for Windows build/test/package work. Backend tests generate OpenCover and TRX; frontend tests generate LCOV. SonarQube imports these reports rather than executing tests. Use trusted CI analysis, disable automatic analysis, keep SONAR_TOKEN in Actions secrets, and never execute fork code using pull_request_target. The mandatory scanner currently requires Sonar authentication: fork/Dependabot runs receive no secret and fail closed before reading source; reviewed changes must be promoted to a trusted branch for tests and analysis. Require trusted build/tests and Sonar quality gate; at least 80% new-code coverage, reviewed security hotspots, and no new blocking quality/security issues. Exclude generated/dependency/build/fixture/test code from production coverage without hiding handwritten logic.

### Scope and evidence limitations

First release is the integrated MVP, followed by full-spec milestones. Exclude initial cloud hosting, SaaS accounts, marketplace, mobile editor, Rumble chat sending, remote OBS control/synchronization, and full StreamElements compatibility. Use original UX/assets and licensed dependencies.

Capture evidence inspected during planning: 785 successful polls, 751 with a livestream, observed recent chat maximum 50, 111 distinct base chat fingerprints, one distinct base Rant fingerprint, and no subscriber/gift examples. These are observations, not a delivery guarantee. Missing live evidence is a capability blocker, never proof of unsupported or supported behavior.

## Goal index


| ID | Goal | Prerequisites | Status |
|---|---|---|---|
| [G00](#g00) | Analyze and sanitize Rumble evidence | None | Complete |
| [G01](#g01) | Create repository and quality infrastructure | G00 | Complete |
| [G02](#g02) | Build application foundation | G01 | Complete |
| [G03](#g03) | Integrate Streamer.bot and Speaker.bot | G02 | Complete |
| [G04](#g04) | Implement reliable Rumble ingestion | G00, G02, G03 | Complete |
| [G05](#g05) | Build overlay runtime and combined chat | G03, G04 | Complete |
| [G06](#g06) | Build basic visual editor and alerts | G05 | Not started |
| [G07](#g07) | Build financial ledger and supporter identities | G04 | Not started |
| [G08](#g08) | Build donor widgets | G05, G07 | Not started |
| [G09](#g09) | Build automation rules | G03, G06, G07 | Not started |
| [G10](#g10) | Deliver and validate the MVP | G01–G09 | Not started |
| [G11](#g11) | Complete advanced editor and built-in widgets | G10 | Not started |
| [G12](#g12) | Deliver custom-widget platform and portability | G11 | Not started |
| [G13](#g13) | Complete compatibility and full-spec qualification | G12 | Not started |

<a id="g00"></a>

## G00 — Analyze and sanitize Rumble evidence

Status: **Complete**
Prerequisites: None

### Deliverables

- Inspect scanner-approved recorder source, README, tests, schema/fields/report and capture database schema. Never expose `.secrets/rumble-url`, embedded Git history, original capture messages or credentials.
- Document every observed field, type, null-only/unobserved structure, candidate event, dedupe limitation and replay design in `docs/rumble-analysis.md` before production Rumble code.
- Derive chronological sanitized fixtures from all 785 SQLite polls; do not assume the existing JSONL contains the full dataset. Consistently replace identities/messages while preserving equality, multiplicity, timestamps, ordering, stream transitions and monetary structure.
- Add synthetic fixtures for new Rants, duplicate occurrences, rotation/reordering, full/no-overlap windows, errors, restart, subscriptions and gifts; label synthetic versus captured provenance.

### Acceptance criteria

- All 785 polls represented in replay with ordering/provenance checks; observed counts reconcile with the report.
- Scanner and explicit publication review find no credentials/private capture content in public artifacts.
- Unknown subscription/gift identity and capture coverage limits are documented; Rumble analysis is committed before production adapter implementation.

### Validation evidence

- 2026-10-01: source/report/database inspection and 88-path inventory documented in `docs/rumble-analysis.md`.
- `python tools/rumble_evidence.py verify --archive references/rumbleLiveAPIScraper.zip`: 785 polls, 88 field paths, full source comparison and private replacement audit passed.
- `python -m unittest discover -s tests/replay -v`: 20 tests passed, including the original recorder's 10 tests executed inside the source qualification test.
- 26 synthetic scenarios generated and structurally qualified; these are future G04 acceptance inputs, not production adapter results.
- Final public-only tree: fixture verifier passed; 18 tests passed and the 2 local-archive tests skipped intentionally. No private reference files were present.
- Every one of the 39 publishable files passed deterministic secrets scanning, including decompressed captured replay and synthetic fixture contents. Source-to-fixture checks verified original private strings were replaced; preserved date/amount metadata is documented explicitly.
- `git check-ignore` verified original ZIP, credential directory, databases/sidecars, build artifacts and Python caches are excluded. Staged paths contain only reviewed G00 documentation/tooling/sanitized fixtures/tests; no production adapter code exists.
- Analysis and evidence committed locally in `a3bd3ad738a2ae11420383e25498310427648883`, before production implementation. Follow-up closure also qualifies new-Rant/expiry timing.
- Runtime: Python 3.14.7 and Sonar CLI 1.9.0. Goal completion audit: `docs/g00-validation.md`.

### Blockers

None for G00. Live subscriber/gift identity, production snapshot-engine acceptance and real bridge/OBS qualification remain explicit gates of later goals, not claimed as complete here.

<a id="g01"></a>

## G01 — Create repository and quality infrastructure

Status: **Complete**
Prerequisites: G00

### Deliverables

- Create public camarokris/tdsblive with fresh main history and MIT license. Publish reviewed SPEC.md, this plan, README/contributing instructions, architecture/security/testing docs and safe gitignore.
- Create backend modules, separate editor/runtime workspaces, streamerbot bootstrap/import/examples, widget packages and unit/integration/replay/fixture test areas. Pin SDK, npm/.NET dependencies, tool versions and Actions revisions.
- Set up windows-2022 CI and meaningful initial tests. Register GitHub-bound SonarQube Cloud project camarokris_tdsblive; configure CI scanner begin/build/test/end, OpenCover/TRX/LCOV paths and SONAR_TOKEN without exposing it.
- Enable dependency updates and available secret protection; protect main with trusted test/build/quality checks.

### Acceptance criteria

- Public repository, license, default branch and safe staged content verified; representative ignore tests pass.
- Initial Windows CI and Sonar analysis succeed, with actual backend/frontend coverage imported.
- Fork-safe workflow behavior and quality-gate requirements verified; no original ZIP, embedded history, private capture or credentials published.

### Validation evidence

- Public MIT repository: https://github.com/camarokris/tdsblive; fresh history contains only reviewed public artifacts and sanitized fixtures.
- Windows run https://github.com/camarokris/tdsblive/actions/runs/36836817528 passed on commit fccfe4e: locked restores, build, 12 .NET tests, 9 frontend tests, lint/type checks, frontend builds and public replay qualification (18 passed; two private-archive tests skipped).
- SonarQube Cloud https://sonarcloud.io/dashboard?id=camarokris_tdsblive is GitHub-bound. CI analysis imported OpenCover and LCOV; all 19 measured production lines across C# and TypeScript have 100% coverage, no test failures/errors, and quality gate OK. Automatic analysis is explicitly disabled.
- Main requires app-bound Windows build and tests and SonarCloud Code Analysis checks with an up-to-date branch. Administrator enforcement, PRs, linear history and resolved conversations are required; force pushes/deletions are disabled.
- GitHub secret scanning, push protection and Dependabot security updates are enabled; Actions default to read-only permissions. Scanner credentials are step-scoped, never published. Fork/Dependabot runs fail closed before source reads and require reviewed promotion to a trusted branch.
- Publication audit scanned 109 source files, decompressed fixtures, checked private-string replacement and representative ignore paths. SDK, dependencies, tools and Actions are pinned; package lockfiles are tracked.
- Detailed evidence, non-blocking static-analysis debt and maintenance obligations: [G01 validation](g01-validation.md).

### Blockers

None for G01. Application features and live integrations remain owned by G02–G13; scaffold coverage does not qualify those capabilities.

<a id="g02"></a>

## G02 — Build application foundation

Status: **Complete**
Prerequisites: G01

### Deliverables

- Implement typed configuration and secret storage, SQLite migrations/indexes, structured redacted logging (14-day default retention), isolated hosted services, startup/shutdown sequencing and diagnostics.
- Implement canonical events, transactional dedupe/checkpoint acceptance, durable outbox and isolated test/replay paths.
- Serve editor shell and documented REST/WebSocket contracts at port 17474; generate frontend API types. Implement loopback default and authenticated optional LAN foundation.

### Acceptance criteria

- Host and editor load over HTTP without certificates; port conflicts are actionable.
- Fresh/migrated databases, rollback/crash/restart, graceful shutdown and idempotent acceptance tests pass.
- Integration failure isolation, credential redaction, origin/host/CSRF checks, and LAN authentication tests pass.

### Validation evidence

- G01 prerequisite revalidated: protected main and successful Windows run 36838097322 on commit 3a16834.
- Foundation branch adds validated typed configuration, UUIDv7/UTC canonical contracts, recursive credential redaction, EF SQLite schema/migration, indexed provenance-scoped dedupe, transactional checkpoint/event/outbox acceptance, and explicit test persistence isolation.
- Local .NET tests pass: 20 core tests and SQLite persistence tests, including reopen/dedupe, raw redaction, transaction rollback and test namespaces. Real Windows DPAPI vault test is intentionally skipped on Linux; Windows CI evidence remains required.
- Host composition now applies migrations before HTTP startup and checkpoints WAL at shutdown. Typed configuration saves atomically; cookie/bearer LAN authentication, host/origin validation, CSRF protection, bounded bodies and login rate limits are wired. Diagnostics/event/configuration/test-event/credential endpoints exist, with redacted structured logs and 14-day default retention.
- Local checks: 31 backend tests passed, one real-Windows DPAPI test skipped. Tests exercise actual ASP.NET middleware, LAN peer authentication/session revocation, persisted configuration loaded into a reopened host, invalid configuration rejection, known-value/URL/assignment redaction and dated log retention.
- Isolated real Kestrel process served status/diagnostics at http://127.0.0.1:17474, rejected an attacker Host (400) and foreign Origin (403). A second process exited 1 with an actionable conflict message. SIGTERM stopped the original process with exit 0, released the port, and left SQLite integrity ok with WAL retained. Test data remained under /tmp, outside repository and normal user data.
- Added isolated integration health/retry services and a 64-row durable outbox worker. Editor WebSockets support filtered subscription/event/ping envelopes, 32 connection slots, 256-frame queues, bounded inbound operations, credential redaction and shutdown cancellation. The API and event contracts document at-least-once/handoff semantics without claiming browser acknowledgments or external exactly-once actions.
- Added `/editor` and `/login` hosting, a status-aware React shell, credential-clearing sign-in, generated OpenAPI snapshot and generated frontend API types. The generator has its own lockfile to isolate its TypeScript 5 peer from application TypeScript 6. Build output remains ignored. Windows CI builds assets before host tests, and those tests verify both shell pages and their JavaScript asset.
- Local suite now passes 35 backend tests (one Windows DPAPI test skipped) and 14 frontend tests with 97.6% frontend line coverage. Socket tests verify filtered durable live delivery, heartbeat, ephemeral simulation without persistence, known-secret scrubbing and integration failure isolation. Overlapping shutdown checkpoint calls are idempotent.
- Real Kestrel served both editor/login HTML and the compiled React asset with HTTP 200. User Chrome blocked the local page with ERR_BLOCKED_BY_CLIENT; protections were not modified and rendered-browser verification remains missing.
- Draft implementation PR: https://github.com/camarokris/tdsblive/pull/3. Local suite now passes 38 backend tests; DPAPI and real-LAN tests are intentionally Windows-only. Eight concurrent acceptances produce one durable event/outbox/checkpoint, pending delivery survives reopen, acknowledgments are idempotent, and initial migration rollback/reapply passes.
- `tools/qualify_foundation.py` launches only temporary-data child processes and verifies HTTP shells, exact OpenAPI drift (excluding runtime server origin), persisted isolated simulation, forced crash, SQLite integrity/redaction, restart, and exclusion from live history. Local qualification passed. Generated frontend types are checked for drift in CI.
- Windows run 36843854629 passed backend/frontend tests and the crash/restart assertions, then failed temporary-directory cleanup because the Python SQLite context did not close its connection. Explicit closure fixes that issue. Final checkpoint now uses a direct SQLite connection, avoiding disposed EF logging services under concurrent host stop. Binary-relative content root and copied editor assets make DLL launch independent of working directory.
- Added a real Windows non-loopback HTTP test using a generated isolated DPAPI admin credential, bearer and cookie authentication, CSRF rejection, local-only provisioning checks and logout. Added a separately locked CI-only Playwright browser qualifier for actual editor/status/login rendering and a non-production screenshot. No personal-browser protection is changed.
- Remaining acceptance: passing updated Windows DPAPI/non-loopback/browser qualification, final security/logging review, trusted CI/Sonar gate and requirement-by-requirement completion audit. G02 remains In progress.
- Windows run [36846286222](https://github.com/camarokris/tdsblive/actions/runs/36846286222) on `65d944c` passed runtime/test qualification but failed Sonar on the intentional HTTP listener and non-Secure cookie. These two findings were explicitly accepted with documented rationale on 2026-10-01 because HTTP, including optional authenticated LAN, is an approved requirement. The PATH-based executable finding was fixed. Credential rotation/login session limits are atomic; log I/O failures are isolated. The initially recorded run 36846290263 was Advanced Security rather than Windows CI and does not prove the Windows quality gate.
- Final persistence audit adds the `FoundationState` migration, authoritative non-secret SQLite configuration with atomic bootstrap fallback, and redacted SQLite logs behind an independent bounded queue. Local tests cover migration from the initial schema without event loss, fallback failure rollback, restart, redaction and retention; local crash/contract qualification also passes. Final Windows requalification and completion audit remain pending.
- Final review adds safe cancellation of disconnected WebSocket subscribers, concurrent disconnect/publish/shutdown qualification, daily log-file pruning and 64-row SQLite log transactions. Local backend suite passes 42 tests with two Windows-only skips. Requirement traceability is maintained in [G02 validation](g02-validation.md). Windows run 36846983150 passed runtime qualification but was superseded/cancelled when the next revision was pushed; it is not recorded as a completed Windows gate.
- Final qualification: [Windows run 36847673559](https://github.com/camarokris/tdsblive/actions/runs/36847673559), commit `0da0b34`, passed all 44 backend tests (including actual Windows DPAPI and non-loopback HTTP), frontend tests, replay checks, crash/restart/OpenAPI qualification, fresh-browser editor/login rendering, generated type checks and the SonarQube quality gate. Imported backend coverage is 91.2%, frontend lines 97.6%, and Sonar new-code coverage 87.0%, with 0% duplication and ratings A. CodeQL and Gitar checks passed. The requirement-by-requirement audit is in [G02 validation](g02-validation.md).
- Two HTTP-specific Sonar findings and two HTTP-cookie CodeQL findings were accepted under the explicit HTTP requirement. Two CodeQL negative-rejection guard findings were reviewed as false positives: early returns deny the entire request, while continuing requests still require authentication/CSRF. No broad security exclusion was added. Twenty-six remaining Sonar code-smell findings are non-blocking under the configured gate; no open vulnerability finding remains. See [security review](security.md).

### Blockers

None for G02 acceptance. Live platform adapters, OBS behavior, visual editing, financial ingestion and installation retain their owning G03–G13 evidence gates.

<a id="g03"></a>

## G03 — Integrate Streamer.bot and Speaker.bot

Status: **Complete**
Prerequisites: G02

### Deliverables

- Implement correlated WebSocket requests, authentication, configurable reconnect and connection health. Discover GetEvents/GetActions/GetCodeTriggers; invoke ExecuteCodeTrigger and selected action GUIDs.
- Provide importable C# Init bootstrap using CPH.RegisterCustomTrigger for Rumble chat/Rant/follow/sub/gift/online/offline/viewer/likes/health and finance/overlay categories. Include argument mapping and explicit forwarding examples for triggers not broadcast over WebSocket.
- Normalize Twitch/YouTube/Kick/Ko-fi events conservatively; retain sanitized unknown diagnostics. Build searchable event inspector with pause/filter/copy/replay/save-fixture and sample payload inspection.
- Implement Speaker.bot documented WebSocket queue protocol; use Streamer.bot actions for VTube Studio. Prevent bridge loops and expose missing actions/uncertain execution.

### Acceptance criteria

- Mocked protocol tests cover auth, discovery, timeout, reconnect, unsupported capability and missing actions.
- Real Streamer.bot bootstrap import/registration and custom-trigger action execution verified with versions recorded.
- Speaker.bot connection/request behavior and forwarding limitations documented; inspector receives real events.

### Validation evidence

- G02 prerequisite verified on protected main `da53c87`: Windows run 36849059398 and Sonar passed.
- Implemented correlated bounded protocol sessions, Streamer.bot authentication/discovery/allowlisted dispatch, independent reconnect, Speaker.bot queue requests, conservative normalization, bridge prevention, bounded inspector, isolated replay, C# bootstrap/forwarding templates, deterministic import generator and editor controls. Public schemas and generated frontend types updated; no migration needed. Contracts and limitations are maintained in [G03 integrations](g03-integrations.md).
- Local backend tests pass: 103 tests, two Windows-only skips. Frontend tests pass (20 tests, 99.0% lines) with inspector/copy/replay and forged identifier rejection coverage. Import transport tests verify deterministic content and artifact-directory confinement.
- [Windows run 36855820795](https://github.com/camarokris/tdsblive/actions/runs/36855820795), revision `43b5037`, passes Windows tests, OpenCover/LCOV import, replay qualification, crash/restart/HTTP contracts, isolated browser rendering and generated type checks. [SonarQube PR analysis](https://sonarcloud.io/dashboard?id=camarokris_tdsblive&pullRequest=4) passes with **85.9% new-code coverage**, 0% duplication, A reliability/security/maintainability, and all hotspots reviewed. CodeQL passes. The first failing gate led to input confinement and reliability refactoring; two specific search-query/port-as-content findings were reviewed as false positives with recorded data-flow rationale, without rule exclusions.
- Live GE-Proton qualification on 2026-10-01 confirmed Streamer.bot **1.0.7**, Speaker.bot **0.1.7** (local port **7580**), all 13 custom-trigger registrations, actual dedicated test-trigger execution and matching synthetic event in the host inspector, safe sample/replay behavior, and Speaker.bot Pause/Resume acknowledgements. Repeat command: `python tools/qualify_bots.py --execute-local-test-trigger-and-queue --speaker-port 7580`. Uses temporary host data and dedicated synthetic actions; no chat/OBS/financial effects. Product default ports are unchanged.

- Final implementation revision `19c01f7` passes [Windows run 36861946605](https://github.com/camarokris/tdsblive/actions/runs/36861946605), including the SonarQube quality gate and OpenCover/LCOV analysis. Local verification passes 107 backend tests (two Windows-only skips), 20 frontend tests, five import tests, type checks, lint and deterministic secrets scanning.
- Operator confirmed the regenerated bundle imports successfully through Streamer.bot's graphical Import dialog on 2026-10-01. The initial matching-version rejection was corrected by removing the optional author gate and retaining the established import-format floor; runtime qualification remains limited to 1.0.7. After restoring the deliberately separate test binding, real trigger execution, inspector reception, isolated replay and Speaker.bot Pause/Resume passed again.
- Automated review fixes contain per-event consumer/ingestion failures without exposing exception details, reject blank forwarded routing, and require an explicit clean native-store scanner verdict before reading. Regression tests verify session survival and refusal to read after ambiguous scan results.
- Delivery: [PR #4](https://github.com/camarokris/tdsblive/pull/4); protected integration and the final evidence revision are traceable through the PR.

### Blockers

None. Compatibility beyond native Streamer.bot 1.0.7 remains unverified and documented; Rumble ingestion and real audio/OBS qualification belong to later goals.

<a id="g04"></a>

## G04 — Implement reliable Rumble ingestion

Status: **Complete**
Prerequisites: G00, G02, G03

### Deliverables

- Implement tolerant parser with redacted unknown-field preservation; poll 7 seconds with positive jitter, minimum 5 seconds, Retry-After and capped exponential error backoff.
- First successful configuration/restart/credential-change/reset poll baselines historical arrays without alerts; current live status is separate. Scope state to account/channel/stream.
- Use complete recent arrays, canonical SHA-256 fingerprints and occurrence reconciliation against preceding windows. Preserve exact text, normalize timestamps/badges; retain message IDs until both older than 24 hours and outside latest 10000. Keep financial dedupe independent.
- Deduplicate Rants using stream/created/user/cents/text/expiry; use amount_cents USD. Follows use context/user/followed timestamp, not counter changes. Track stats and health changes.
- Offline requires two consecutive successful offline observations per stream; failures do not advance debounce. Full-window zero overlap emits possible_gap diagnostics, never invented messages.
- Implement documented subscription fixtures with unverified-live labeling. Preserve gift shapes; do not treat remaining_gifts mutations as purchases. Disable authoritative gift automation/financial ingestion pending qualifying identity evidence.

### Acceptance criteria

- Full capture replay and synthetic error/rotation/multiplicity/restart fixtures pass without duplicate emissions or baseline alerts.
- Timeout/429/500/malformed JSON cause health changes without false offline; successful recovery resumes polling.
- Real Rumble-to-Streamer.bot trigger execution verified; missing live subscription/gift validation remains explicitly gated.
- Ambiguous identical records and snapshot loss limits documented without claiming complete delivery.

### Validation evidence

- Prerequisites revalidated on protected main `fb860e0`: G00 analysis/fixtures present, G02 migrations/contracts intact, G03 merged in PR #4; [Windows main run 36864616946](https://github.com/camarokris/tdsblive/actions/runs/36864616946) passed.
- Implemented bounded HTTPS transport, typed parser, functional snapshot engine, account/channel/stream state, persistent multiset reconciliation, baseline suppression, monetary uniqueness, debounced offline/health/statistics/gap diagnostics and independent durable trigger delivery. Migration `20261001131537_RumbleIngestion`, public OpenAPI/generated types, local credential/polling controls and compatibility rules are documented in [G04 ingestion](g04-rumble-ingestion.md).
- All 785 captured polls passed through the production .NET parser/engine and temporary SQLite store: 97 post-baseline chat events, four follows, no new repeated Rant, one online/offline transition each, no false zero-overlap gap. All 26 synthetic scenarios passed through the same implementation, including reopened persistent stores, error/debounce cases and explicitly labeled subscription/gift limits.
- Regression coverage verifies transactional checkpoint/state/event/outbox rollback, uncertain trigger claims after crash, late bootstrap discovery refresh, chat retention, permanent Rant identity across credential rotation, timeout/429/500/malformed/oversized HTTP behavior, CSRF-protected session/Windows credentials and isolated replay. The queue regression parks 64 unavailable viewer events without blocking a newer health event, then resumes matching registrations across SQLite reopen. Long Retry-After scheduling uses bounded cancellable timers.
- Implementation revisions `134fb3c` and `4319d05` are reviewed in [PR #5](https://github.com/camarokris/tdsblive/pull/5). [Windows run 36875988152](https://github.com/camarokris/tdsblive/actions/runs/36875988152) passed on `4319d05`: 170 backend tests (no Windows skips), 26 frontend tests, 27 Python replay/import checks (two unavailable private-archive checks skipped), type/lint, generated contract consistency, crash/HTTP qualification and rendered editor checks. OpenCover and LCOV imported successfully; production coverage is 94.4% backend and 99.3% frontend. [SonarQube PR quality gate](https://sonarcloud.io/dashboard?id=camarokris_tdsblive&pullRequest=5) passed with 91.2% new-code coverage; CodeQL and automated review checks passed. The identified queue starvation review thread was resolved after the regression passed.
- 2026-10-01 actual Rumble polling via operator-entered session-only credential established a healthy baseline. A canonical Rumble health event was acknowledged by Streamer.bot 1.0.7 and its namespaced synthetic receipt matched the original event ID, proving action execution. The account was offline; no live chat, paid Rant, subscription or gift interaction is claimed. Qualification uses temporary data and receipt-only bindings, never production financial totals.
- Final-build qualification on `4319d05` repeated the real healthy baseline and matching receipt (canonical event received at `2026-10-01T14:24:55Z`). The session credential was then deleted through the protected endpoint (204, presence false) and the temporary host stopped. The credential file remained unread; private references, generated imports, decompressed fixtures and reports remain ignored and untracked.
- Nonblocking quality findings remain visible in SonarQube: parser/engine/dispatcher/import-helper complexity, parameter/literal cleanup, async buffer writing and native status-element accessibility. These are maintainability follow-ups, not suppressed findings or evidence of verified subscription/gift support; the configured quality gate passes.

### Blockers

None for the G04 acceptance criteria. Unverified live subscriptions and authoritative gifts remain explicitly gated by design; the offline live check does not establish paid or chat behavior. Snapshot identity/loss limitations and nonblocking maintainability findings are recorded above and in the owning integration document.

<a id="g05"></a>

## G05 — Build overlay runtime and combined chat

Status: **Complete**
Prerequisites: G03, G04

### Deliverables

- Implement separate lightweight transparent OBS runtime, local assets, one shared WebSocket per overlay, bounded delivery/DOM, filtered subscriptions and reconnect.
- Normalize Twitch/YouTube/Kick/Rumble chat with platform/user/message/raw metadata. Provide configurable icons/avatar/badges/name/timestamp/colors/fonts/duration/max messages/animations/ignore users/prefixes/bots, scrolling and persistent modes.
- Validate/deduplicate assets by SHA-256; serve IDs rather than filesystem paths. Sanitize SVG and validate MIME/size.
- Provide a responsive streamer chat page usable in an OBS custom browser dock or ordinary browser, with a persistent light/dark toggle and the same normalized feed.
- Preserve and render Streamer.bot supplied Twitch, 7TV, BetterTTV and FrankerFaceZ emote artwork and explicit Twitch GIF parts, with safe URLs, bounded rendering and text fallbacks. Record unavailable live GIF evidence explicitly.

### Acceptance criteria

- OBS displays transparent combined chat, with repeated Rumble snapshots rendered once.
- Supported platform flows, reconnect and filtering verified; editor libraries absent from runtime bundle.
- Upload/traversal/SVG and bounded-chat tests pass; actual OBS rendering verified.

### Validation evidence

Implemented runtime, settings panel, assets, migration, scoped read-only tokens and streamer chat; owning contract and operating instructions: [G05 overlays and chat](g05-overlays-chat.md). G03/G04 completion and successful main Windows CI run 36878683352 establish prerequisites.

2026-10-01: 217 backend tests passed locally; the Windows-only cases also pass in CI. All 49 frontend tests, type checking, lint, lightweight runtime build, fresh isolated browser qualification and foundation process qualification passed. Runtime JavaScript is approximately 72.1 kB gzipped, with editor dependency exclusion enforced by the build. Existing 785-poll Rumble replay tests pass.

Real installed Streamer.bot → Twitch → isolated host → streamer browser chat verified by the user's test message and confirmation that it appears once and Light/Dark works. Backend history independently contains exactly one matching message. YouTube/Kick are documented-payload/synthetic qualification only. The user also confirmed actual OBS transparent source and dock/theme behavior, then reported label-only badges. Badge artwork preservation/rendering is corrected, tested and confirmed by a fresh user Twitch message. Added Twitch/7TV/BetterTTV/FrankerFaceZ emote and GIF rendering is tested but native Twitch/BTTV/FFZ/7TV rendering is verified by the user and all four sources appear in normalized history. The initial live GIF exposed query-URL redaction; the narrow public-CDN fix passes normalization, persistence and socket tests. The user confirmed the live GIF rendered but reported its description also appearing. The caption correction preserves accessible alt/hover text, hides the visible duplicate in new and existing messages, and passes local backend/frontend/browser checks. The user refreshed the real view and confirmed the GIF animates without the duplicate label. [Windows CI run 36897123880](https://github.com/camarokris/tdsblive/actions/runs/36897123880) passed on functional revision `7f1e86481041c68ed11cc2b16951487727870322`, including Windows tests, replay, fresh-browser qualification and OpenCover/LCOV import. The SonarQube Cloud PR quality gate passed with 92.2% new-code coverage, A security/reliability/maintainability ratings, zero new duplication and all required hotspots reviewed. CodeQL and automated review pass; the buffer-filter restoration review thread is resolved.

### Blockers

None for G05 acceptance. Live YouTube/Kick accounts were unavailable; their documented shapes and synthetic flows are validated without claiming live qualification. Live Rumble-to-OBS chat was not observed; the actual Rumble reconciliation/store/socket replay verifies one rendered event across repeated snapshots, alongside G04's live API baseline and integration evidence. Twitch, badges, all four emote providers, animated GIFs, actual OBS transparency/dock and theme switching are operator verified. G06 and later goals remain not started.

<a id="g06"></a>

## G06 — Build basic visual editor and alerts

Status: **Not started**
Prerequisites: G05

### Deliverables

- Provide canvas presets including vertical/custom, layers, drag/resize/properties, basic snapping, keyboard controls and undo/redo.
- Implement 750ms autosave, conflict detection, 50 revision default, restore, preview and copy OBS URL.
- Deliver Text/Image/media, Combined Chat and AlertBox presets with templates, sound/video/animation; bounded queues with priority/duration/cooldown/group/concurrency/interrupt policy.
- Expose synthetic test event controls and developer raw injection with isolation defaults.

### Acceptance criteria

- Playwright verifies create/add/drag/resize/save/reload/restore/preview and repeated interactions.
- Alert queue concurrency/cooldown/overflow and OBS sound/media behavior verified.
- Test events never invoke production financial or external automation by default.

### Validation evidence

None recorded. Planning inspection is not implementation acceptance.

### Blockers

None identified for starting prerequisite work. Any acceptance evidence unavailable during implementation must be recorded here.

<a id="g07"></a>

## G07 — Build financial ledger and supporter identities

Status: **Not started**
Prerequisites: G04

### Deliverables

- Ingest Bits, donations, Super Chats/Stickers where exposed, subscriptions/gifts/memberships, Kick support, Ko-fi and Rumble Rants using adapter strategies and durable uniqueness.
- Correlate gift batches/individual notifications to avoid double-counting; do not infer unsupported monetary values.
- Use integer minor amounts and decimal rates; retain native currency/rate/date/provider and exact/fx/configured_nominal/unknown methods. Setup leaves Bits/sub nominal rules unconfigured until chosen.
- Use historical Frankfurter rates, cache/manual dated overrides, visibly estimated latest-rate fallback, pending unavailable conversion and explicit reconciliation; freeze accepted historical values.
- Provide manual cross-platform identity linking and indexed current-stream/today/week/month/year/all-time/custom aggregation with configured timezone.

### Acceptance criteria

- Precision/rounding, missing FX, freeze/reconcile, nominal/unknown labels, reconnect/replay dedupe and gift correlation tested.
- Manual identity linking changes totals without name-based automatic merging.
- DST, Monday-week and custom/current-stream boundary tests pass; unverified gift accounting remains gated.

### Validation evidence

None recorded. Planning inspection is not implementation acceptance.

### Blockers

None identified for starting prerequisite work. Any acceptance evidence unavailable during implementation must be recorded here.

<a id="g08"></a>

## G08 — Build donor widgets

Status: **Not started**
Prerequisites: G05, G07

### Deliverables

- Implement Donor Crown, ranked leaderboard (1–25), latest supporter and current-stream leader.
- Provide period/platform/event/minimum filters, name/avatar/badge/amount/crown visibility, templates/fonts/colors/assets and animated leader transitions.
- Push aggregate changes after financial/identity/valuation updates without browser refresh.

### Acceptance criteria

- Live rankings/crown update correctly for new entries, links and explicit reconciliation.
- Empty/pending/unknown/estimated data is represented honestly; filters and period changes tested.
- Actual OBS donor-widget rendering verified.

### Validation evidence

None recorded. Planning inspection is not implementation acceptance.

### Blockers

None identified for starting prerequisite work. Any acceptance evidence unavailable during implementation must be recorded here.

<a id="g09"></a>

## G09 — Build automation rules

Status: **Not started**
Prerequisites: G03, G06, G07

### Deliverables

- Implement event filters and exact/minimum/range/multiple conditions, multiple actions, cooldowns, queues and execution tracking.
- Implement Ko-fi TTS templates with minimum amount, voice, max length, URL/punctuation/repetition/bad-word controls and moderation options.
- Play sound assets in OBS with volume/queue/interrupt/cooldown/random variants/ducking metadata.
- Execute selected Streamer.bot VTS actions; support timed reversion via toggle or enable/disable actions and Extend/Restart/Ignore/Queue policies.
- Keep financial ingestion independent of alert/rule configuration; expose ambiguous external outcomes without blind retry.

### Acceptance criteria

- Rule boundaries, queues, timer stacking/restart, failures and isolated simulation tested.
- Real Ko-fi-path Speaker.bot speech, OBS sound capture and Streamer.bot-to-VTS behavior verified.
- Missing actions/voices, moderation and uncertain execution remain visible.

### Validation evidence

None recorded. Planning inspection is not implementation acceptance.

### Blockers

None identified for starting prerequisite work. Any acceptance evidence unavailable during implementation must be recorded here.

<a id="g10"></a>

## G10 — Deliver and validate the MVP

Status: **Not started**
Prerequisites: G01–G09

### Deliverables

- Build self-contained Windows x64 ZIP and Inno Setup installer in Actions; attach checksums after release validation.
- Implement first-run connection/bootstrap/Rumble/Speaker/timezone/valuation/overlay wizard and optional login startup; ordinary runtime unelevated.
- Provide SQLite-safe backup/validated restore with safety backup and paused consumers, secret-free config export/import, installation/recovery docs.
- Validate integrated MVP, authenticated LAN HTTP, rendering/audio, restart and the specified performance workload on a documented Windows streaming PC.

### Acceptance criteria

- All G00–G09 acceptance gates satisfied; Windows CI and Sonar quality gate pass.
- Installer/portable build verified on Windows; setup requires no normal-user command line or certificates.
- Real end-to-end Rumble trigger/chat/Rant ledger/crown and Ko-fi/Bits automation flows work together.
- Measure idle <1% CPU, ordinary-chat <3% average CPU and backend <300MB against documented hardware/workload; record deviations honestly.

### Validation evidence

None recorded. Planning inspection is not implementation acceptance.

### Blockers

None identified for starting prerequisite work. Any acceptance evidence unavailable during implementation must be recorded here.

<a id="g11"></a>

## G11 — Complete advanced editor and built-in widgets

Status: **Not started**
Prerequisites: G10

### Deliverables

- Add rotation, multi-select, grouping/ungrouping, copy/paste/duplicate, lock/hide, alignment/distribution, z-order, grid/snap, zoom/pan and keyboard nudging.
- Complete Event List, Goal/Progress bars and remaining specified media/supporter widgets.
- Complete typed settings fields: text/textarea/number/slider/check/dropdown/multiselect/color/font/assets/duration/event/action/user/platform/button/hidden/group.

### Acceptance criteria

- Browser tests exercise repeated advanced interactions, undo/redo and save/reload with groups/transforms.
- Every specified built-in widget/settings capability has requirement evidence.
- OBS runtime remains lightweight and existing MVP workflows pass regression checks.

### Validation evidence

None recorded. Planning inspection is not implementation acceptance.

### Blockers

None identified for starting prerequisite work. Any acceptance evidence unavailable during implementation must be recorded here.

<a id="g12"></a>

## G12 — Deliver custom-widget platform and portability

Status: **Not started**
Prerequisites: G11

### Deliverables

- Implement Monaco HTML/CSS/JS/settings editor, versioned package manifests and schema-driven settings/subscriptions.
- Run custom code in allow-scripts iframes without same-origin/top-navigation privileges; default external networking disabled with explicit domain permissions.
- Expose mediated SBX lifecycle/event/session/config/store API, validate messages against specific frames/capabilities, isolate widget storage and grant raw/financial/chat/audio permissions explicitly.
- Export/import .sbxoverlay and .sbxwidget ZIP containers with manifest/assets and no secrets/absolute paths; enforce size/decompression/traversal limits.

### Acceptance criteria

- Custom widget binds arbitrary available events and stores state across restart; permissions enforced.
- Sandbox escape, parent access, network, token disclosure and message spoofing tests pass.
- Portable packages round-trip; malicious traversal/oversized packages rejected.

### Validation evidence

None recorded. Planning inspection is not implementation acceptance.

### Blockers

None identified for starting prerequisite work. Any acceptance evidence unavailable during implementation must be recorded here.

<a id="g13"></a>

## G13 — Complete compatibility and full-spec qualification

Status: **Not started**
Prerequisites: G12

### Deliverables

- Implement limited local StreamElements shim: onWidgetLoad/onEventReceived/onSessionUpdate, fieldData/listener/event and safe store/queue/status equivalents. Unsupported calls give descriptive warnings.
- Deliver migration/replay/sample generation/backup/log sanitization/diagnostic export utilities and unknown-shape inspector with sanitized fixture export.
- Complete architecture/events/rumble/overlay/widget/database/security/testing/user documentation and requirement matrix for every numbered source-spec section.
- Qualify performance and compatibility; classify each requirement verified, deferred by explicit scope or blocked by evidence.

### Acceptance criteria

- Compatibility fixtures and unsupported-call behavior tested; no full-SE compatibility claim.
- Diagnostics/utilities protect credentials and private data; performance evidence recorded.
- All G00–G12 complete, every original requirement traced, and no hidden unfinished acceptance gate remains.

### Validation evidence

None recorded. Planning inspection is not implementation acceptance.

### Blockers

None identified for starting prerequisite work. Any acceptance evidence unavailable during implementation must be recorded here.

## Official reference documentation

- [Streamer.bot API](https://docs.streamer.bot/api)
- [Streamer.bot WebSocket requests](https://docs.streamer.bot/api/websocket/requests)
- [Streamer.bot examples](https://docs.streamer.bot/examples)
- [Streamer.bot client](https://github.com/streamerbot/client)
- [Speaker.bot WebSocket API](https://speaker.bot/api/websocket)
- [StreamElements overlays](https://docs.streamelements.com/overlays)
- [Rumble Live Stream API](https://rumble.support/en/help/how-to-use-rumble-s-live-stream-api)
- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [SonarQube .NET coverage](https://docs.sonarsource.com/sonarqube-cloud/analyzing-source-code/test-coverage/dotnet-test-coverage)
- [SonarQube JS/TS coverage](https://docs.sonarsource.com/sonarqube-cloud/analyzing-source-code/test-coverage/javascript-typescript-test-coverage)
- [Frankfurter](https://frankfurter.dev/)

Recheck version-sensitive protocols and dependencies during their owning goal. Observed capture shapes take priority over guessed Rumble structures; official examples supplement unobserved cases without replacing evidence.
