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

## Approved scope change — VTube Studio on hold

On 2026-10-02 the operator placed all VTube Studio-specific work in this plan on hold and selected Streamer.bot's built-in VTube Studio integration. This applies across all goals: native VTube Studio clients, dedicated VTube Studio mappings/UI/templates, model/hotkey discovery, temporary clothing/effect policies and real-model qualification are deferred until explicitly resumed. These deferred requirements do not block G09, MVP or full-plan completion under the revised scope, and must never be reported as verified.

Generic Streamer.bot action discovery, stable action IDs, allowlisted dispatch, durable execution tracking and safe simulation remain in scope. Existing generic dispatch/timer code and tests are retained; no migration or deletion is required. Users may configure VTube Studio actions directly in Streamer.bot. Preserve the original specification and goal IDs as historical references; this scope decision governs their VTube Studio-specific clauses.

## Locked architecture and defaults

| Item | Decision |
|---|---|
| Repository | Public `techdaddykb/tdsblive`, fresh history, default branch `main` |
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

GitHub account renamed to `TechDaddyKB`; repository links and Git origin use `techdaddykb/tdsblive`. SonarQube service identifiers remain unchanged to preserve analysis history. G08 PR analysis verified the renamed GitHub binding; its protected delivery evidence is recorded below.

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
| [G06](#g06) | Build basic visual editor and alerts | G05 | Complete |
| [G07](#g07) | Build financial ledger and supporter identities | G04 | Complete |
| [G08](#g08) | Build donor widgets | G05, G07 | Complete |
| [G09](#g09) | Build automation rules | G03, G06, G07 | Complete |
| [G10](#g10) | Deliver and validate the MVP | G01–G09 | Complete |
| [G11](#g11) | Complete advanced editor and built-in widgets | G10 | Complete |
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

- Create public techdaddykb/tdsblive with fresh main history and MIT license. Publish reviewed SPEC.md, this plan, README/contributing instructions, architecture/security/testing docs and safe gitignore.
- Create backend modules, separate editor/runtime workspaces, streamerbot bootstrap/import/examples, widget packages and unit/integration/replay/fixture test areas. Pin SDK, npm/.NET dependencies, tool versions and Actions revisions.
- Set up windows-2022 CI and meaningful initial tests. Register GitHub-bound SonarQube Cloud project camarokris_tdsblive; configure CI scanner begin/build/test/end, OpenCover/TRX/LCOV paths and SONAR_TOKEN without exposing it.
- Enable dependency updates and available secret protection; protect main with trusted test/build/quality checks.

### Acceptance criteria

- Public repository, license, default branch and safe staged content verified; representative ignore tests pass.
- Initial Windows CI and Sonar analysis succeed, with actual backend/frontend coverage imported.
- Fork-safe workflow behavior and quality-gate requirements verified; no original ZIP, embedded history, private capture or credentials published.

### Validation evidence

- Public MIT repository: https://github.com/techdaddykb/tdsblive; fresh history contains only reviewed public artifacts and sanitized fixtures.
- Windows run https://github.com/techdaddykb/tdsblive/actions/runs/36836817528 passed on commit fccfe4e: locked restores, build, 12 .NET tests, 9 frontend tests, lint/type checks, frontend builds and public replay qualification (18 passed; two private-archive tests skipped).
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
- Draft implementation PR: https://github.com/techdaddykb/tdsblive/pull/3. Local suite now passes 38 backend tests; DPAPI and real-LAN tests are intentionally Windows-only. Eight concurrent acceptances produce one durable event/outbox/checkpoint, pending delivery survives reopen, acknowledgments are idempotent, and initial migration rollback/reapply passes.
- `tools/qualify_foundation.py` launches only temporary-data child processes and verifies HTTP shells, exact OpenAPI drift (excluding runtime server origin), persisted isolated simulation, forced crash, SQLite integrity/redaction, restart, and exclusion from live history. Local qualification passed. Generated frontend types are checked for drift in CI.
- Windows run 36843854629 passed backend/frontend tests and the crash/restart assertions, then failed temporary-directory cleanup because the Python SQLite context did not close its connection. Explicit closure fixes that issue. Final checkpoint now uses a direct SQLite connection, avoiding disposed EF logging services under concurrent host stop. Binary-relative content root and copied editor assets make DLL launch independent of working directory.
- Added a real Windows non-loopback HTTP test using a generated isolated DPAPI admin credential, bearer and cookie authentication, CSRF rejection, local-only provisioning checks and logout. Added a separately locked CI-only Playwright browser qualifier for actual editor/status/login rendering and a non-production screenshot. No personal-browser protection is changed.
- Remaining acceptance: passing updated Windows DPAPI/non-loopback/browser qualification, final security/logging review, trusted CI/Sonar gate and requirement-by-requirement completion audit. G02 remains In progress.
- Windows run [36846286222](https://github.com/techdaddykb/tdsblive/actions/runs/36846286222) on `65d944c` passed runtime/test qualification but failed Sonar on the intentional HTTP listener and non-Secure cookie. These two findings were explicitly accepted with documented rationale on 2026-10-01 because HTTP, including optional authenticated LAN, is an approved requirement. The PATH-based executable finding was fixed. Credential rotation/login session limits are atomic; log I/O failures are isolated. The initially recorded run 36846290263 was Advanced Security rather than Windows CI and does not prove the Windows quality gate.
- Final persistence audit adds the `FoundationState` migration, authoritative non-secret SQLite configuration with atomic bootstrap fallback, and redacted SQLite logs behind an independent bounded queue. Local tests cover migration from the initial schema without event loss, fallback failure rollback, restart, redaction and retention; local crash/contract qualification also passes. Final Windows requalification and completion audit remain pending.
- Final review adds safe cancellation of disconnected WebSocket subscribers, concurrent disconnect/publish/shutdown qualification, daily log-file pruning and 64-row SQLite log transactions. Local backend suite passes 42 tests with two Windows-only skips. Requirement traceability is maintained in [G02 validation](g02-validation.md). Windows run 36846983150 passed runtime qualification but was superseded/cancelled when the next revision was pushed; it is not recorded as a completed Windows gate.
- Final qualification: [Windows run 36847673559](https://github.com/techdaddykb/tdsblive/actions/runs/36847673559), commit `0da0b34`, passed all 44 backend tests (including actual Windows DPAPI and non-loopback HTTP), frontend tests, replay checks, crash/restart/OpenAPI qualification, fresh-browser editor/login rendering, generated type checks and the SonarQube quality gate. Imported backend coverage is 91.2%, frontend lines 97.6%, and Sonar new-code coverage 87.0%, with 0% duplication and ratings A. CodeQL and Gitar checks passed. The requirement-by-requirement audit is in [G02 validation](g02-validation.md).
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
- Implement Speaker.bot documented WebSocket queue protocol and generic Streamer.bot action dispatch. VTube Studio-specific work is on hold under the approved scope change. Prevent bridge loops and expose missing actions/uncertain execution.

### Acceptance criteria

- Mocked protocol tests cover auth, discovery, timeout, reconnect, unsupported capability and missing actions.
- Real Streamer.bot bootstrap import/registration and custom-trigger action execution verified with versions recorded.
- Speaker.bot connection/request behavior and forwarding limitations documented; inspector receives real events.

### Validation evidence

- G02 prerequisite verified on protected main `da53c87`: Windows run 36849059398 and Sonar passed.
- Implemented correlated bounded protocol sessions, Streamer.bot authentication/discovery/allowlisted dispatch, independent reconnect, Speaker.bot queue requests, conservative normalization, bridge prevention, bounded inspector, isolated replay, C# bootstrap/forwarding templates, deterministic import generator and editor controls. Public schemas and generated frontend types updated; no migration needed. Contracts and limitations are maintained in [G03 integrations](g03-integrations.md).
- Local backend tests pass: 103 tests, two Windows-only skips. Frontend tests pass (20 tests, 99.0% lines) with inspector/copy/replay and forged identifier rejection coverage. Import transport tests verify deterministic content and artifact-directory confinement.
- [Windows run 36855820795](https://github.com/techdaddykb/tdsblive/actions/runs/36855820795), revision `43b5037`, passes Windows tests, OpenCover/LCOV import, replay qualification, crash/restart/HTTP contracts, isolated browser rendering and generated type checks. [SonarQube PR analysis](https://sonarcloud.io/dashboard?id=camarokris_tdsblive&pullRequest=4) passes with **85.9% new-code coverage**, 0% duplication, A reliability/security/maintainability, and all hotspots reviewed. CodeQL passes. The first failing gate led to input confinement and reliability refactoring; two specific search-query/port-as-content findings were reviewed as false positives with recorded data-flow rationale, without rule exclusions.
- Live GE-Proton qualification on 2026-10-01 confirmed Streamer.bot **1.0.7**, Speaker.bot **0.1.7** (local port **7580**), all 13 custom-trigger registrations, actual dedicated test-trigger execution and matching synthetic event in the host inspector, safe sample/replay behavior, and Speaker.bot Pause/Resume acknowledgements. Repeat command: `python tools/qualify_bots.py --execute-local-test-trigger-and-queue --speaker-port 7580`. Uses temporary host data and dedicated synthetic actions; no chat/OBS/financial effects. Product default ports are unchanged.

- Final implementation revision `19c01f7` passes [Windows run 36861946605](https://github.com/techdaddykb/tdsblive/actions/runs/36861946605), including the SonarQube quality gate and OpenCover/LCOV analysis. Local verification passes 107 backend tests (two Windows-only skips), 20 frontend tests, five import tests, type checks, lint and deterministic secrets scanning.
- Operator confirmed the regenerated bundle imports successfully through Streamer.bot's graphical Import dialog on 2026-10-01. The initial matching-version rejection was corrected by removing the optional author gate and retaining the established import-format floor; runtime qualification remains limited to 1.0.7. After restoring the deliberately separate test binding, real trigger execution, inspector reception, isolated replay and Speaker.bot Pause/Resume passed again.
- Automated review fixes contain per-event consumer/ingestion failures without exposing exception details, reject blank forwarded routing, and require an explicit clean native-store scanner verdict before reading. Regression tests verify session survival and refusal to read after ambiguous scan results.
- Delivery: [PR #4](https://github.com/techdaddykb/tdsblive/pull/4); protected integration and the final evidence revision are traceable through the PR.

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

- Prerequisites revalidated on protected main `fb860e0`: G00 analysis/fixtures present, G02 migrations/contracts intact, G03 merged in PR #4; [Windows main run 36864616946](https://github.com/techdaddykb/tdsblive/actions/runs/36864616946) passed.
- Implemented bounded HTTPS transport, typed parser, functional snapshot engine, account/channel/stream state, persistent multiset reconciliation, baseline suppression, monetary uniqueness, debounced offline/health/statistics/gap diagnostics and independent durable trigger delivery. Migration `20261001131537_RumbleIngestion`, public OpenAPI/generated types, local credential/polling controls and compatibility rules are documented in [G04 ingestion](g04-rumble-ingestion.md).
- All 785 captured polls passed through the production .NET parser/engine and temporary SQLite store: 97 post-baseline chat events, four follows, no new repeated Rant, one online/offline transition each, no false zero-overlap gap. All 26 synthetic scenarios passed through the same implementation, including reopened persistent stores, error/debounce cases and explicitly labeled subscription/gift limits.
- Regression coverage verifies transactional checkpoint/state/event/outbox rollback, uncertain trigger claims after crash, late bootstrap discovery refresh, chat retention, permanent Rant identity across credential rotation, timeout/429/500/malformed/oversized HTTP behavior, CSRF-protected session/Windows credentials and isolated replay. The queue regression parks 64 unavailable viewer events without blocking a newer health event, then resumes matching registrations across SQLite reopen. Long Retry-After scheduling uses bounded cancellable timers.
- Implementation revisions `134fb3c` and `4319d05` are reviewed in [PR #5](https://github.com/techdaddykb/tdsblive/pull/5). [Windows run 36875988152](https://github.com/techdaddykb/tdsblive/actions/runs/36875988152) passed on `4319d05`: 170 backend tests (no Windows skips), 26 frontend tests, 27 Python replay/import checks (two unavailable private-archive checks skipped), type/lint, generated contract consistency, crash/HTTP qualification and rendered editor checks. OpenCover and LCOV imported successfully; production coverage is 94.4% backend and 99.3% frontend. [SonarQube PR quality gate](https://sonarcloud.io/dashboard?id=camarokris_tdsblive&pullRequest=5) passed with 91.2% new-code coverage; CodeQL and automated review checks passed. The identified queue starvation review thread was resolved after the regression passed.
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

Real installed Streamer.bot → Twitch → isolated host → streamer browser chat verified by the user's test message and confirmation that it appears once and Light/Dark works. Backend history independently contains exactly one matching message. YouTube/Kick are documented-payload/synthetic qualification only. The user also confirmed actual OBS transparent source and dock/theme behavior, then reported label-only badges. Badge artwork preservation/rendering is corrected, tested and confirmed by a fresh user Twitch message. Added Twitch/7TV/BetterTTV/FrankerFaceZ emote and GIF rendering is tested but native Twitch/BTTV/FFZ/7TV rendering is verified by the user and all four sources appear in normalized history. The initial live GIF exposed query-URL redaction; the narrow public-CDN fix passes normalization, persistence and socket tests. The user confirmed the live GIF rendered but reported its description also appearing. The caption correction preserves accessible alt/hover text, hides the visible duplicate in new and existing messages, and passes local backend/frontend/browser checks. The user refreshed the real view and confirmed the GIF animates without the duplicate label. [Windows CI run 36897123880](https://github.com/techdaddykb/tdsblive/actions/runs/36897123880) passed on functional revision `7f1e86481041c68ed11cc2b16951487727870322`, including Windows tests, replay, fresh-browser qualification and OpenCover/LCOV import. The SonarQube Cloud PR quality gate passed with 92.2% new-code coverage, A security/reliability/maintainability ratings, zero new duplication and all required hotspots reviewed. CodeQL and automated review pass; the buffer-filter restoration review thread is resolved.

### Blockers

None for G05 acceptance. Live YouTube/Kick accounts were unavailable; their documented shapes and synthetic flows are validated without claiming live qualification. Live Rumble-to-OBS chat was not observed; the actual Rumble reconciliation/store/socket replay verifies one rendered event across repeated snapshots, alongside G04's live API baseline and integration evidence. Twitch, badges, all four emote providers, animated GIFs, actual OBS transparency/dock and theme switching are operator verified. G06 and later goals remain not started.

<a id="g06"></a>

## G06 — Build basic visual editor and alerts

Status: **Complete**
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

G05 prerequisite verified against merged commit `8053c99eb12edfbca4628c7f82201c82324a9e9e` and successful main Windows CI run 36900637540. Implementation is delivered on `g06-visual-editor-alerts`; owning contracts, queue semantics, HTTP/security boundaries and scope are recorded in [G06 editor and alerts](g06-editor-alerts.md). Local checks pass: 227 backend tests (two Windows-only skips), 88 frontend tests, type checking/lint, build (runtime 74.3 kB gzipped), and foundation process/contract qualification. Frontend line coverage is 96.27%. Fresh isolated Playwright verifies create/add/drag/resize/nudge, repeated duplicate/delete/undo/redo, 750ms save/reload/restore, served GIF, actual VP9 decoding and muted PCM audio playback, embedded chat, synthetic/native alert previews, inherited group settings, single socket, and no durable injection history. Backend tests cover transaction rollback, concurrent stale-version rejection, 50-revision retention, validation, scoped LAN media and rejection of test events by normal/wrong-overlay viewers; scheduler tests cover groups/priority/FIFO/concurrency/cooldown/overflow/interrupt behavior. Windows CI run 36908584159 passed build/tests/replay but exposed a Playwright restore timing assumption; qualification now waits for the real restore receipt. Automated review also found group-setting inheritance and historical-chat alert replay issues; both are corrected with tests. Windows CI run 36910749328 passed build/tests/replay/browser and imported 83.3% new-code coverage with A security; Sonar flagged three reliability findings (promise handling, reduce initialization and CSS font fallback). They are fixed; the subsequent Windows/SonarQube run below passed.

The final local canvas audit passed at commit `c6478b5135b5ff7f2e87ee12df54a7ef5c71135d`: reconnect retains one open socket and does not replay completed alerts; the clipboard contains the actual OBS URL; a separately connected source updates after revision restore and removes obsolete media/chat widgets. Windows CI run [36915186896](https://github.com/techdaddykb/tdsblive/actions/runs/36915186896) passed build, backend/frontend tests, replay, recovery, browser qualification and SonarQube quality gate for that commit. Logs confirm OpenCover and LCOV import; production coverage is 95.5% backend and 96.3% frontend. SonarQube reports 87.8% new-code coverage, A reliability/security/maintainability, zero new duplication, and all security hotspots reviewed.

The operator confirmed on 2026-10-01: “OBS Playback of alert video and audio was successful,” after the isolated 1280×720 Browser Source test at `/overlay/g06-qualification?preview=1&audio=1`. This supplies real OBS media/audio evidence alongside the browser-tested bounded duration and cleanup. All G06 acceptance criteria are verified; G11/G12/G09 scope boundaries remain unchanged.

### Blockers

None. The operator's successful OBS video/audio confirmation is recorded above. G06 was delivered through protected [PR #7](https://github.com/techdaddykb/tdsblive/pull/7), merged on 2026-10-01 at 20:13:38 UTC as `232c59e23e28a63e8d1f349f16c5114bacbd28e8`. No further OBS confirmation is pending for G06.

<a id="g07"></a>

## G07 — Build financial ledger and supporter identities

Status: **Complete**
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

Implemented and merged through protected [PR #8](https://github.com/techdaddykb/tdsblive/pull/8) as `734cf7c8e972b7411f0b7a235081b348f3ad26fa` on 2026-10-02 UTC (2026-10-01 local). G04 prerequisite PR #5 is merged and verified in the implementation ancestry. The [requirement-by-requirement audit](g07-qualification.md) maps every G07 deliverable and inherited security/test contract to actual source, test and runtime evidence. [Ledger contracts](g07-financial-ledger.md) document APIs, migrations, valuation rules and compatibility boundaries.

The trusted [Windows run](https://github.com/techdaddykb/tdsblive/actions/runs/36944795513) at source `29fce9658b44917ae9d6207c9507021b388fa70c` passes **40 core tests, 261 host tests with no skips, and 102 frontend tests**, plus sanitized replay, actual crash/restart qualification, populated-ledger browser interactions and generated API-type verification. OpenCover and LCOV were imported by SonarQube; the [quality gate](https://sonarcloud.io/dashboard?id=camarokris_tdsblive&pullRequest=8) passed with **89.5% new-code coverage**, A reliability/security/maintainability ratings, 0% new duplication and 100% hotspot review. The workflow production-coverage verifier reports 96.1% backend and 93.7% frontend coverage. CodeQL and PR review checks pass; the ingestion rejection finding is fixed and regression-tested.

Actual isolated Release checks pass live Frankfurter lookup, preserved rate/date/provider, restart integrity, acknowledged-event catch-up, frozen historical valuations, duplicate native-ID suppression, selected reconciliation and simulation isolation. Real browser checks pass manual identity link/unlink, combined totals, exact rule/manual-rate strings, settings/reload and custom periods. Installed Streamer.bot read-only broadcaster discovery and synthetic Ko-fi CPH forwarding are verified without payment or production financial writes. Owned documented-contract fixtures combine all 11 required support families and test gift arrival orders, conflicts, recipient exclusion, precision, pending FX, DST and rollback. HTTP remains supported with no HTTPS requirement.

### Blockers

None for G07. Authoritative Rumble gift accounting remains explicitly gated as required. Paid-platform live observations are not claimed; documented-contract tests and actual installed forwarding checks are identified separately. Donor widgets and financial automation remain owned by G08 and G09.

<a id="g08"></a>

## G08 — Build donor widgets

Status: **Complete**
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

Implementation was delivered in [PR #10](https://github.com/TechDaddyKB/tdsblive/pull/10), on `feat/g08-donor-widgets` from G07 delivery commit `87efc67`. Requirements were checked against SPEC sections 29–31, 84 and the Phase 9 widget list. Contracts, filtered exact SQL aggregation, shared-socket delivery, runtime rendering, assets, editor controls and leader transitions are implemented; acceptance and protected delivery are verified below.

Final local checks at `b6ea8f2` pass 51 core tests, 277 host tests (two Windows-only skips), and 121 frontend tests, plus typecheck, lint, builds and deterministic tracked/new-file secrets scans. A 20,031-contribution SQLite qualification verifies exact amounts beyond 64-bit range, at most 25 ranked rows, a ten-second query budget, and parameterized source-filter behavior. Scoped donor assets cannot grant access to financial administration. OpenAPI and generated frontend types are refreshed. These checks do not establish G08 completion.

The disposable Release-host browser qualification passes donor rendering for all five kinds, live identity link/unlink, platform filtering, explicit reconciliation, current-stream total changes, editor persistence/revision restore, reconnect and preview isolation. The same run passes preceding G02/G05/G06/G07 browser checks. The latest built browser also verifies decoded owned crown imagery, successful owned WOFF loading, template/color/size changes, and old-to-new fade transitions after identity changes. The first two Windows runs failed at test-host cleanup with locked files; those failures are recorded, not counted as successful gates. Run `36956960049` passed backend tests but exposed stale initial editor loading; the ordering fix passes deterministic StrictMode and overlapping-selection regressions. The final Windows run [`36958513495`](https://github.com/TechDaddyKB/tdsblive/actions/runs/36958513495) passes 51 core/279 host tests without skips, 121 frontend tests, replay/recovery/browser qualification, generated types and OpenCover/LCOV import. Its Sonar gate fails only on S2077 dynamic SQL (88.6% new coverage, zero duplication, all hotspots reviewed). The query is now a static statement with parameterized JSON filter lists; 18 targeted donor tests pass. A fresh passing gate is still required. Sonar PR analysis now points to the renamed GitHub repository, verifying the existing service binding. The next run `36959527247` failed at final temporary-database deletion after passing the financial assertions. A Windows-only two-second cleanup retry now has transient/persistent handle tests; persistent locks remain failures. Local checks pass 51 core/277 host tests with four Windows-only skips. Actual Windows cleanup and the fixed-query gate still require a successful run. An owned OBS source is running at `http://127.0.0.1:17475/overlay/donor-obs-check`; the operator subsequently confirmed rendering and live refresh. Public contracts are recorded in [donor widgets](g08-donor-widgets.md).

The trusted [Windows run 36960170546](https://github.com/TechDaddyKB/tdsblive/actions/runs/36960170546), source `a38ce7faefb39607359e0ae630a7082a552fcc58`, passes **51 core tests, 281 host tests without skips, and 121 frontend tests**, plus replay/recovery, actual browser qualification and generated contract checks. Logs verify OpenCover and LCOV import and 96.2% backend/94.3% frontend production coverage. The [SonarQube PR gate](https://sonarcloud.io/dashboard?id=camarokris_tdsblive&pullRequest=10) passes with **88.5% new-code coverage**, A reliability/security/maintainability, zero duplication and all hotspots reviewed. The fixed SQL finding is closed automatically; no exclusion or dismissal is used. CodeQL passes and no unresolved PR review findings are reported. See the maintained [qualification audit](g08-qualification.md).

The documentation evidence commit `0c8a7644b8ce6d5f60be547b31a577fd38f50f84` also passes [Windows run 36961251786](https://github.com/TechDaddyKB/tdsblive/actions/runs/36961251786) and its exact-head Sonar gate, with no unresolved vulnerabilities or PR review findings.

Final corrected source `2d143c56525caa311c842eab23ebddbe8d4879d8` passes [Windows run 36964748598](https://github.com/TechDaddyKB/tdsblive/actions/runs/36964748598): 51 core and 281 host tests without skips, all 131 frontend tests, replay/recovery, full browser qualification and generated contracts. Scanned logs verify OpenCover and LCOV import, 96.2% backend/94.3% frontend production coverage and a passing Sonar gate with 88.7% new-code coverage, A ratings, zero duplication and all hotspots reviewed. CodeQL passes. The operator confirms actual OBS rendering, live refresh and corrected platform logos. Protected [PR #10](https://github.com/TechDaddyKB/tdsblive/pull/10) merged at 2026-10-02 04:42:31 UTC as `9deb7c2b7aebbe401c22cfdf9ffaea4427f73d02`, with exact-head checks passing and no bypass.

### Blockers

None. Actual OBS rendering, refresh and platform-logo confirmation are recorded above. Unverified Rumble gift behavior remains gated as required; it is not claimed supported by these widgets.

<a id="g09"></a>

## G09 — Build automation rules

Status: **Complete under the approved scope**
Prerequisites: G03, G06, G07

### Deliverables

- Implement event filters and exact/minimum/range/multiple conditions, multiple actions, cooldowns, queues and execution tracking.
- Implement Ko-fi TTS templates with minimum amount, voice, max length, URL/punctuation/repetition/bad-word controls and moderation options.
- Play sound assets in OBS with volume/queue/interrupt/cooldown/random variants/ducking metadata.
- Execute selected generic Streamer.bot actions. **On hold:** VTube Studio-specific mappings and timed model-effect reversion via toggle or enable/disable actions and Extend/Restart/Ignore/Queue policies; Streamer.bot's built-in integration is the selected route.
- Keep financial ingestion independent of alert/rule configuration; expose ambiguous external outcomes without blind retry.

### Acceptance criteria

- Rule boundaries, queues, timer stacking/restart, failures and isolated simulation tested.
- Real Ko-fi-path Speaker.bot speech and OBS sound capture verified. VTube Studio live-model qualification is on hold and excluded from the active acceptance gates.
- Missing actions/voices, moderation and uncertain execution remain visible.

### Validation evidence

Requirements inspected against SPEC sections 32–36, 38, 61–62. G03/G06/G07 prerequisites are delivered on main; G08 delivery is also present. Rule contracts, transactional inbox/planning, durable execution receipts, timers, browser sound commands and the editor are implemented on the G09 branch. Ko-fi privacy/language metadata and explicit review are covered by tests; integration diagnostics expose unavailable dependencies and unverified voice aliases. Detailed evidence and limitations are maintained in [G09 automation](g09-automation.md).

The 2026-10-02 full local run passed 75 core, 293 integration and 138 frontend tests. Four integration tests require Windows (DPAPI and file-handle cleanup) and were skipped locally. The subsequent capability-endpoint checks passed two targeted HTTP tests. These results do not establish real speech, OBS sound capture, VTube Studio state, Windows CI or Sonar qualification.

Subsequent local validation passed 311 integration tests (four Windows-only skips), 139 frontend tests and 35 targeted automation cases covering persisted stacking/recreation, independent queues, interruption, queued-effect receipt identity, cancellation and disable/delete handling. Draft [PR #12](https://github.com/TechDaddyKB/tdsblive/pull/12) is open. Its initial [Windows run](https://github.com/TechDaddyKB/tdsblive/actions/runs/36975597976) passed build/tests/qualification but failed the Sonar gate at 75.5% new-code coverage versus the required 80%. Final-head coverage and quality qualification remain incomplete.

The sound protocol/security additions pass 14 real WebSocket/HTTP cases; broader local runs pass 325 integration tests (four Windows-only skips) and 153 frontend tests. An isolated host confirmed actual Speaker.bot 0.1.7 connectivity on its configured port 7580 without speech/queue side effects. The second Windows run at `b8d1b87` failed only its Sonar coverage condition at 77.0%; new sound tests require another analysis. Captured audio, configured voice and VTube Studio effects are not claimed.

Commit `5ec62fba322cfaaa2064089b5f08810dce860e8c` subsequently passed [Windows CI and its Sonar gate](https://github.com/TechDaddyKB/tdsblive/actions/runs/36979682918). Named canvas/audio selection and reusable browser qualification are published at `69589976b0caf6ad0a569b47a1b2f0a882825961`; 154 frontend tests, lint, type checking and the full isolated browser suite pass locally. Browser checks verify save/reload/edit/delete and simulation without execution receipts or financial writes. That newer head's [Windows run](https://github.com/TechDaddyKB/tdsblive/actions/runs/36981696581) is still running.

An explicitly opted-in owned event in a marked temporary G09 database traversed the durable automation inbox, live rule dispatcher, overlay WebSocket and real Chromium audio player. One sound command was received and its durable execution completed. External integrations were disabled; the enabled qualification rule was then disabled. This proves browser playback of generated WAV audio, not OBS capture. A separate transparent OBS source is prepared at `http://127.0.0.1:17476/overlay/g09-sound-qualification` for operator verification.

The maintained [G09 qualification audit](g09-qualification.md) maps the full requirement scope to inspected evidence and explicitly lists remaining live and exact-head delivery checks.

Run 36981696581 subsequently finished: all builds/tests/qualification passed and new-code coverage reached 82.3%, but the Sonar gate failed reliability finding `javascript:S2871` in the newly added browser qualification's default `sort()`. An explicit ID comparator addresses that finding; final-head CI must pass before delivery.

The correction passed [Windows run 36983149942](https://github.com/TechDaddyKB/tdsblive/actions/runs/36983149942) at `796f75a8ee38bdf6cbda28da5035bb34b3d1d5b3`: builds, tests, replay/recovery, full browser qualification, generated contracts, OpenCover/LCOV import and Sonar gate. Scanned logs report 95.8% backend/89.8% frontend production coverage; the gate reports 82.4% new-code coverage, A ratings, zero duplication and 100% reviewed hotspots. An additional owned-fixture failure check verifies that rejected inbox insertion rolls back event and outbox writes, and a mismatched platform/type is refused.

The scope/evidence documentation run 37005887684 failed an existing Rumble late-registration test under Windows coverage: its ten-second cancellation deadline expired while processing a 64-row parked backlog, and failure bypassed worker shutdown before SQLite cleanup. The test now uses a bounded 60-second deadline and always cancels/awaits its owned workers in `finally`, preserving all delivery/reopen assertions. The targeted local test passes. No production retry, timeout or delivery behavior was changed; the final Windows gate remains required.

Final implementation head `b8dc63c6f1160cb422f6d2dd838865dcba5c0a06` passes [Windows run 37007511716](https://github.com/TechDaddyKB/tdsblive/actions/runs/37007511716): 75 core and 329 host tests with no skips, frontend tests, replay/recovery, browser qualification, generated contracts, OpenCover/LCOV import and the Sonar gate. Scanned logs report 95.7% backend/89.8% frontend production coverage; Sonar reports 82.3% new-code coverage, A ratings, zero duplication and 100% reviewed hotspots. The operator confirmed actual OBS sound and Speaker.bot speech with `local english`. All active acceptance criteria are verified. The completion-record commit is documentation only and [PR #12](https://github.com/TechDaddyKB/tdsblive/pull/12) must pass its normal exact-head protected checks before merging; no bypass is authorized or used.

### Blockers

None for the active G09 scope. VTube Studio-specific requirements remain explicitly on hold, delegated to Streamer.bot's built-in integration and not claimed as verified. Normal protected checks govern delivery of the documentation-only completion record.

<a id="g10"></a>

## G10 — Deliver and validate the MVP

Status: **Complete**
Prerequisites: G01–G09

Current acceptance audit: [release qualification checklist](g10-qualification.md).

### Deliverables

- Build self-contained Windows x64 ZIP and Inno Setup installer in Actions; attach checksums after release validation.
- Implement first-run connection/bootstrap/Rumble/Speaker/timezone/valuation/overlay wizard and optional login startup; ordinary runtime unelevated.
- Provide explicit read-only bot connection tests in setup, distinguishing successful protocol responses from disabled, rejected or timed-out tests; connection tests do not execute speech, queue changes or automation. Rumble connection/baseline checks use the operator-entered URL.
- Provide SQLite-safe backup/validated restore with safety backup and paused consumers, secret-free config export/import, installation/recovery docs.
- Publish a complete, plain-language release guide in the repository and its GitHub wiki, with screenshots from isolated owned examples where available. Cover installation, first setup, everyday use, Streamer.bot/Speaker.bot/Rumble connections, OBS sources/docks, chat, overlays/alerts, supporter totals, automation, LAN HTTP, backup/restore, updates, troubleshooting and uninstall. Explain unfamiliar terms, use numbered steps and expected results, and avoid assuming command-line knowledge. Keep developer/API references separate and link them from the guide. Validate instructions against the actual packaged application; keep screenshot credentials and private content out of public artifacts.
- Validate integrated MVP, authenticated LAN HTTP, rendering/audio and restart in the available Wine/Proton environment, with native Windows packaging and installer checks in GitHub Actions. Native Windows streaming-PC performance qualification is explicitly deferred by the operator; it is not a G10 completion blocker under this approved scope.

### Acceptance criteria

- All G00–G09 acceptance gates satisfied; Windows CI and Sonar quality gate pass.
- Installer/portable build verified on Windows; setup requires no normal-user command line or certificates.
- Repository guide and wiki are published, with working navigation/images and matching instructions. An unfamiliar user can follow the documented setup path without using developer tools.
- Rumble trigger/chat/Rant ledger/crown and Ko-fi/Bits automation flows work together in the available environment. Under the operator-approved paid-event scope below, owned examples qualify paid-event ingestion, totals and local automation; actual paid-platform delivery remains explicitly unverified.
- Measure against idle <1% CPU, ordinary-chat <3% average CPU and backend <300MB targets using documented hardware/workload; record deviations honestly. Operator-approved Wine exception (2026-10-02): release with documented CPU and memory deviations from these targets, without claiming a performance pass. Distinguish Wine/Proton measurements from native Windows CI results. Native Windows streaming-PC performance remains deferred and must not be presented as verified.

### Validation evidence

Operator-approved qualification scope: only the current Wine/Proton environment is available. The operator selected “Qualify Wine/Proton plus Windows CI; explicitly defer streaming-PC performance.” This deferral applies solely to native Windows streaming-PC performance; packaging, installer, integrated behavior and available-environment performance checks remain required.

Operator-approved paid-event scope (2026-10-02): “Use owned examples and explicitly leave actual paid-platform delivery unverified.” No test payments are required. Exercise reviewed synthetic Rants and owned Ko-fi/Bits examples through production libraries and the packaged runtime in disposable data, with explicit local-action opt-in. Keep this evidence distinct from real Rumble API health and Streamer.bot forwarding/trigger receipts. Do not claim real Rant, donation or cheer delivery, subscriber/gift compatibility, or effects on production financial totals from these examples.

### Current acceptance evidence

- MVP [v0.1.0](https://github.com/TechDaddyKB/tdsblive/releases/tag/v0.1.0) is public; G00–G09 prerequisites and protected G10 delivery are complete under the approved scope.
- Final Windows run `37073729890` / `649b2d1` passes tests, OpenCover/LCOV import, SonarQube and native packaging. The actual downloaded EXE passes real-browser bot probes under Wine and graceful quit; all uploaded digests and sizes match verified artifacts.
- Real Rumble polling reports healthy with its baseline established. Actual OBS rendering/reconnect and the operator's “Heard both” confirm fresh Ko-fi speech and Bits audio through the saved owned rules.
- Reviewed owned examples qualify persistent Rumble dedupe, exact/nominal totals, crown/ranking and local automation. Actual paid-platform delivery remains unverified by explicit scope.
- Ten plain-language guide chapters/eight owned screenshots are published on main and wiki `3913754`; the final ZIP's complete offline guide passes with networking disabled.
- With real Rumble polling, both bots and OBS connected, Wine measures 4.616% idle/5.600% chat CPU (one core = 100%) and peak backend RSS 321.35 MB. The operator accepts these documented CPU/memory deviations; targets are not claimed as passed. Native Windows streaming-PC performance remains deferred.
- Authenticated LAN HTTP, recovery, optional startup and installed-bot behavior have the distinct real-Wine/native-CI evidence recorded in the qualification checklist. HTTPS is not required.

### Historical implementation checkpoints

The following records preserve intermediate results. Current acceptance evidence
above supersedes earlier statements that a now-verified check was still pending.

Recovery checkpoint (2026-10-02): 27 local recovery/archive/restore and browser-launch checks passed. Recovery checks exercise SQLite WAL snapshots, archive integrity and asset validation, actual managed-host shutdown, retained safety copies, disabled restored connections/rules, suppressed stale deliveries, and revoked overlay tokens. Backup configuration reads are bounded to 64 KiB; temporary backup/upload files use owner-only Unix permissions. Recovery endpoints, user controls and native packaged restart/restore qualification remain incomplete; these checks do not prove the complete recovery workflow.

Recovery controls checkpoint: the editor now exposes backup download, check-before-restore with explicit replacement confirmation, connection-settings export/import, restart and quit. A draft plain-language recovery guide accompanies it. Two UI tests verify confirmation gating, invalidation when the selected file changes, and the owner-only error message. Twenty-six backend recovery/lifecycle tests pass, including backup API round-trip, malformed ZIP rejection, unconfirmed/unknown restore rejection, request protection, one-operation concurrency and relaunch argument/error handling. Failed restore does not automatically launch another host. Full browser, authenticated remote-owner restrictions, expiry and native packaged restart/restore qualification remain pending.

Packaging foundation: a self-contained `win-x64` publish profile, separate locked Windows dependency graphs, per-user Inno Setup script, checksum generation and native Windows package qualification are implemented for CI evaluation. `dotnet restore src/ExtensionSuite.Host/ExtensionSuite.Host.csproj --runtime win-x64 --locked-mode -p:NuGetLockFilePath=packages.win-x64.lock.json` passed locally; this verifies dependency reproducibility, not a Windows build. Eight browser-launch unit cases passed locally. Native package qualification remains pending its Actions run. These are release candidates, not a completed MVP release.

Guide checkpoint: canonical Home and Chat/OBS pages, three owned screenshots and a scan-before-copy wiki synchronization tool are added. Wiki commit `fc519c4` publishes those illustrations; browser inspection verified all three images loaded. Eleven browser-launch unit/startup cases passed, including flag-off and failed browser launch with a healthy host. Frontend type checking/lint and 19 targeted chat/settings/logo tests passed. The rebuilt full isolated browser qualifier passed editor/chat/visual-editor/finance/donor/automation checks after the readability and chat-logo changes. The remainder of the guide and G10 functionality are still pending.

G01–G09 prerequisites are delivered on main, including protected G09 merge `26bb2eeea61ad5d4fbe6435f91778996de02d926`. G10 implementation is isolated on `feat/g10-mvp-release` in draft PR #13. The operator's VTube Studio scope hold remains in force. The wiki was initialized and its in-progress Home and Chat/OBS guide published at wiki commit `4ac015c`; the complete guide and illustrations remain to deliver. Initial Windows run `37014293135` passed its build/tests/qualifiers but failed the new-code coverage gate (70.8% versus 80%), so package steps were not reached. Browser-launch startup integration cases are being added to cover this path. Windows packaging, setup, backup/restore and integrated/performance qualification remain to implement and verify.

Additional recovery validation: 34 combined backend recovery/lifecycle checks passed locally. Seven authenticated LAN endpoint cases prove owner-only recovery/settings/process operations return 403 while ordinary authenticated status remains available. An actual prepared archive verifies expiry at the 15-minute boundary, old-stage deletion on replacement, rejection of stale confirmation IDs and ownership transfer of queued restore data through lifecycle disposal. Native packaged behavior remains to qualify.

Setup checkpoint: a six-step guided editor flow now covers welcome, bot connections, Rumble, supporter timezone/valuation, OBS chat and a final review. Bot address/port forms save through the existing validated configuration API. This is an implementation draft: persistent first-run progress, credential entry, bootstrap verification, optional-startup controls and browser qualification remain required. It must not be treated as a completed setup wizard.

Setup form validation: two UI tests pass for navigation without automatic writes and connection saving while preserving unrelated settings and the forwarding flag. Connection forms load saved addresses before enabling edits. Type checking and lint are checked separately; these mocked UI tests do not establish real bot connectivity.

Setup persistence checkpoint: `/api/setup` stores versioned progress in SQLite; unfinished setup opens automatically and resumes the saved step. One backend integration test verifies request protection, invalid/stale update rejection, progress across a new host instance and explicit review completion. The two updated UI tests and lint pass. Review completion is an operator preference, not evidence that integrations passed. Credential/bootstrap/startup controls and actual browser/package qualification remain pending.

Credential setup checkpoint: guided setup includes a Streamer.bot password form, session-only by default and optional Windows DPAPI persistence. Submitted values are cleared from the input and omitted from responses/configuration. Type checking and both existing setup UI tests pass. Real authentication/reconnect, persistent Windows credential behavior, credential removal and bootstrap/startup qualification remain required.

Integrated local checkpoint: all 158 frontend tests passed; the complete host suite reported 370 passed and four skipped (the skipped checks are not claimed as verified). Rebuilt frontend assets and the Release host build succeeded with zero warnings/errors. The full fresh-browser qualifier passed existing chat/editor/finance/donor/automation checks plus first-run review persistence across reload, unchanged disabled integrations after navigation, real backup download/upload validation and explicit restore-confirmation gating. This browser run intentionally does not request shutdown or restore; native packaged process restart/recovery and live integrations remain separate gates.

Real-process recovery checkpoint: `node tools/browser-qualification/recovery-process.mjs` passed against the rebuilt local Release host in an owned temporary data directory. HTTP restart and restore each produced a new process generation; restart retained later setup data, restore recovered the earlier backup, restored connections/LAN were disabled and quit stopped the host. Editor-opening intent is preserved on relaunch so isolated hosts do not launch a personal browser. This is local Linux managed-process evidence, not Windows-package or Wine qualification; those checks remain required.

Public contract checkpoint: refreshed `docs/contracts/openapi.json` from an isolated Release host after secrets scanning, regenerated frontend client types and documented setup/recovery/credential behavior in `docs/foundation-api.md`. Frontend type checking passed. The full foundation process qualifier passed HTTP shells, contract drift, crash recovery, redaction, restart and test isolation. These local checks do not replace final Windows CI/Sonar checks.

User-guide checkpoint: canonical installation/update/uninstall and backup/recovery chapters are linked from Home and published to the wiki at commit `f9934ea`. The synchronizer validated all four page links and three existing screenshots before copying; only the reviewed guide pages and sidebar were staged. Candidate/qualification warnings remain explicit. These chapters cover the current implementation; complete release documentation and packaged-instruction verification remain outstanding.

Expanded guide checkpoint: the repository guide now has ten pages and five owned screenshots covering first setup, overlays/alerts, supporter totals, automation, LAN and troubleshooting. Wiki commit `efb3a9a` publishes all ten pages. A standalone offline HTML renderer builds the same guide; local link checks and a fresh-browser page/image/navigation check pass, with visual inspection. Packaging includes the offline guide and an installer Start menu shortcut. Package qualification now requires guide files and exercises restart/restore via portable and installed EXEs; these new Windows checks await a new Actions head and are not yet verified.

Wine preflight (2026-10-02): Wine 11.17 Staging is available; no bot Wine processes were running during inspection. The earlier Windows candidate from `590a73d` was decompressed into an isolated directory and scanned before execution. In a fresh owned Wine prefix, its host did not reach HTTP readiness; the scanned log reported `System.IO.FileNotFoundException` loading `System.Runtime.dll`. Only owned prefix processes were stopped. This is a failed compatibility preflight of the earlier candidate, not a final current-build or native Windows result. Wine qualification remains unresolved; Windows Actions run `37026932245` targets `d0baeac` separately.

Wine preflight correction: the first launch disabled Wine's `mscoree` loader through a test-only override. Retesting the earlier candidate with the standard `mscoree` loader reached HTTP readiness with its unique owned application name and created its own SQLite database. The first failure was caused by that test configuration; it is not evidence of application incompatibility. Owned prefix processes were stopped afterward. Final-current-candidate Wine integration/performance qualification remains outstanding.

Windows run `37026932245` failed one hosted Rumble test (373 host tests passed). Its fixture invented a new message on every HTTP poll, allowing the background worker to introduce chat between reset assertions. The fixture now advances its snapshot explicitly while still exercising the real hosted poller and reset. This requires a new exact-head Windows run; the failed run did not reach package qualification or Sonar completion.

Setup/package gap closure: Windows packaging now generates `integrations/tdsblive-streamerbot.sb` from individually scanned C# sources and requires it in portable/installed package checks. Decompressed generated-import inspection confirms the three reviewed sources, stable distinct action IDs, no autorun action and no platform bindings. Wiki commit `3306fc9` publishes matching import/offline-guide instructions. Bot integrations now offers action allowlist and live-forwarding controls through the editor, with seven related UI tests passing; these controls require restart and do not execute actions. Five maintenance UI tests pass for confirmation, settings import and process-operation gating. Actual import and package checks of the final release candidate remain pending.

Quality-gate repair checkpoint: Windows run `37027780281` passed the new-code coverage condition (80.9%) but failed its security rating on unrestricted qualification-tool paths. Offline guide output is now constrained beneath the repository's ignored `release` directory; packaged process qualification permits only `TDSBLive.exe` in the owned Windows runner's UUID-named portable/installed test directories. Local real-process restart/restore still passes, guide generation succeeds in the allowed directory and rejects an external destination, and the full frontend coverage run passes with 88.8% line coverage. The corrected head still requires Windows CI and Sonar analysis; these local results do not prove the security gate has cleared.

Release qualification checkpoint: Windows run `37030077956` completed real-process restart/restore assertions, then failed while deleting an owned SQLite WAL file before Windows released its handle. Cleanup now retries bounded transient file locks and still fails on persistent locks; the updated local process check passes. Native package checks now also verify startup is off by default, the opt-in shortcut targets the installed EXE without opening the editor, and uninstall removes that shortcut; Windows execution remains pending. Fresh-browser qualification passes real permission/LAN settings persistence without enabling integrations and the full existing editor/chat/finance/donor/automation suite. The public wiki's ten chapters loaded and all five existing screenshots decoded in a fresh browser. Three additional owned screenshots illustrate the visual editor, empty supporter ledger and disabled Ko-fi speech-rule draft; financial/automation form spacing is improved and the same browser suite passes after rebuilding. Streamer.bot MCP now confirms version 1.0.7, HTTP/WebSocket connectivity, all three TDSBLive actions and thirteen custom triggers; this discovery alone does not qualify final packaged end-to-end execution. Speaker.bot and final Wine/native package qualification are still required.

Windows run `37031475607` passed managed-process recovery and cleanup, but its SonarQube gate still failed security findings in the executable-path qualification interface. New-code coverage passed at 82.7%, duplication at 0.5%, and all hotspots were reviewed. The offline-guide path finding is resolved. Packaged recovery qualification now accepts only literal `portable`/`installed` modes and derives a fixed executable path from the runner-owned package directory, rather than accepting an executable argument. Managed recovery still passes locally; an arbitrary executable-path argument is rejected before any temporary test data is created. A new exact-head analysis is required. Wiki commit `48900b9` publishes all eight screenshots, and fresh browser checks verify all ten chapters and all eight images in both wiki and offline HTML.

Windows run `37033816613` passed SonarQube with A ratings, 82.7% new coverage, 0.5% duplication and 100% hotspot review, then failed package guide generation because the child deterministic scanner lacked its authenticated CI environment. Packaging/native qualification now receive the same conditionally supplied scanner credentials as the existing trusted scan steps; fork builds do not receive those secrets. Decompressed/installed package contents are scanned before inspection. Local Codex/VS Code MCP configuration is now ignored, and the visual editor offers manual OBS-URL copying when HTTP browsers omit or deny clipboard access. Three clipboard-environment regression cases are added. Final exact-head Windows packaging and Wine qualification remain pending.

Windows run `37037428506` passed analysis and authenticated guide-source scanning, then exposed a Windows-only UTF-8 decoding failure in offline guide generation. Markdown reads now specify UTF-8 explicitly, matching generated HTML. This is a packaging correction, not evidence that native package checks or final Wine acceptance have passed; a new exact-head run is required.

Windows run `37040945969` passed backend tests, then failed one frontend video-volume assertion because DOM creation preceded the playback effect. The media test now waits for the required volume/mute/loop state rather than merely the element's presence, also verifying updated loop settings. The corrected local coverage run passes all 170 tests with 88.93% line coverage and LCOV output; a new exact-head Windows pipeline remains required. This failed run did not reach final package qualification.

Windows run `37041732834` / `da50bd5` completed successfully, including SonarQube, ZIP/installer production, native packaged restart/restore, opt-in startup, reinstall/uninstall and retained user data. Downloaded artifacts pass deterministic scanning and checksum verification. The shipped EXE starts under Wine, but its CSRF endpoint fails at ASP.NET's default CNG SP800-108 provider. An owned Windows diagnostic proves DPAPI round-trip succeeds and the supported managed protector succeeds while the default protector fails at that provider. The host now selects managed AES-256-CBC/HMAC-SHA256 without disabling CSRF or credential protection. Five local host security tests pass, including tamper and purpose-isolation rejection. Corrected Wine runtime checks and a new exact-head Windows run remain required.

Corrected local Windows publish under Wine passes real LAN HTTP sign-in, scoped viewing-link authorization/revocation, remote owner-only restrictions, credential rotation and DPAPI persistence across restart. Actual EXE restart/restore recovers earlier setup progress, records a retained safety directory and pauses restored integrations. Installed Streamer.bot/Speaker.bot connections, dedicated trigger receipt, isolated replay and CPH chat/Ko-fi forwarding pass; simulated support is excluded from the ledger. This is locally published Windows/Wine evidence, distinct from final Actions artifacts, OBS/audible output and performance checks still pending.

Windows run `37044495358` / `ff330f2` completed successfully with managed protection. Local Wine profiling with connected bots and OBS preview measures 4.216% idle CPU (60 seconds), 5.183% ordinary synthetic chat CPU (180 seconds, two messages/second), and peak RSS 288.32 MB, using one logical core as 100%. CPU targets are not met; a short Warning-logging comparison did not improve CPU, so suppressing diagnostics is not a demonstrated remedy. Available-environment performance remains open.

An actual restart with the OBS browser source open exposed a shutdown deadline failure: the final database checkpoint received a cancelled token and raised `TaskCanceledException`. The operator's scanned, ignored Wine backtrace is consistent with the captured managed crash. Browser subscriptions now cancel on `ApplicationStopping` before Kestrel's graceful wait. The real-process qualifier holds an overlay browser open and checks reconnection after restart and restore; it passes locally. The corrected local Windows EXE also passes Wine recovery with OBS present and no managed crash in its captured console. The new exact-head Windows pipeline and remaining final-package/performance/integrated evidence are still required.

Windows run `37046606099` / `679f855` completes successfully, including SonarQube and browser-connected native package recovery. Scanned downloaded ZIP and installer match checksums. All ten offline guide chapters and eight images render from that actual ZIP. Its EXE passes Wine authenticated LAN HTTP, rotation, persisted credentials and restricted/revoked viewing tokens. The local Windows publish passes combined real-browser editor/media, simulation, financial precision, identity linking, donor updates/appearance and reconnect; actual OBS screenshots show chat after recovery and PNG alpha confirms transparency. The Actions EXE passes initial restart/restore but crashes on a subsequent restart, with an overlay request lasting roughly 30 seconds. The first cancellation snapshot does not cover a handshake that finishes afterward. Shutdown now retains a terminal state and rejects/cancels late connections; six local socket tests pass, including a deterministic delayed handshake. Corrected Wine and exact-head Windows qualification remain required before release; G10 stays In progress.

Final candidate checkpoint (2026-10-02): Windows run `37049814743` / `d3a01e9541d35da5f78e0b5294b11f16a45191cc` passes tests, SonarQube, native packaging and browser-connected restart/restore. Scanned downloaded ZIP/installer checksums match. The actual Actions EXE under Wine passes authenticated LAN, restart/restore, the previously failing additional bot-config restart, real bot trigger/CPH forwarding and the full browser visual/media/automation-simulation/financial/donor/reconnect suite. The late-handshake regression is corrected.

Owned paid-event integrated checkpoint: production libraries from that exact package accept one 125-cent reviewed synthetic Rant and one owned Rumble chat after baseline, with persistent repeat-snapshot dedupe. Packaged workers project the Rant, a $5 Ko-fi example and 100 nominal one-cent Bits into exactly three contributions totaling 725 cents. Actual OBS screenshots show one Rumble chat, the crown changing from $1.25 to the $5 Ko-fi supporter, ranked supporters and the $7.25 total with estimates marked. Exactly two owned automation receipts record Speaker.bot dispatch through `local english` and OBS browser sound completion; the owned rules were disabled afterward. Audible confirmation, final available-environment measurements, final guide/release checks and actual API health remain pending. Actual paid-platform delivery remains explicitly unverified under the approved scope.

Guide correction checkpoint: final-package inspection found external GitHub Markdown links rewritten as `.html`. The builder now rewrites only local chapter links. A real-browser check passes all ten corrected offline chapters, eight images, navigation, UTF-8 and the preserved remote plan URL. Native package checks assert local-link resolution and the external plan link. Wiki commit `0b98615` publishes the approved owned paid-event limitation. Corrected guide packaging and final release publication remain pending.

Final-candidate performance checkpoint: actual `d3a01e9` Actions EXE with connected bots, two owned OBS browser sources, Info logging and disabled test rules measures 4.449% idle CPU over 60.02 seconds and 5.383% chat CPU over 180 seconds (360 synthetic preview messages, two/second); one logical core is 100%. Peak backend RSS is 297.14 MB. The memory target passes in this workload; CPU targets do not. Rumble is disabled pending local session credential entry, so enabled polling is not covered. All-32-processor normalization is separately reported as 0.1390%/0.1682%, not used to silently replace the one-core comparison. OBS and wineserver measurements are recorded separately in the qualification checklist. This is available Wine evidence, not native Windows streaming-PC performance.

Source-spec completion audit: section 86's explicit bot connection-test buttons were missing. They now issue correlated read-only metadata requests and truthfully report disabled/rejected/timeout states without speech, queue changes, actions or configuration writes. Seven backend cases and eight setup UI cases pass; actual installed Streamer.bot/Speaker.bot answer both buttons in a real isolated browser with empty execution histories. Full local tests pass 175 frontend (88.97% line coverage) and 380 host cases, with four Windows-only skips. Refreshed OpenAPI/generated types pass actual-process contract/recovery checks. Section 91's ten required documentation entry points are present, with seven new canonical-guide/contract indexes; public Markdown links resolve. Stale G08/G09 index/delivery statements and architecture placeholders are corrected using merged PR evidence. Wiki commit `e017419` publishes new test instructions; ten offline chapters/eight images pass browser checks. Run `37054493247` passes analysis/SonarQube but fails native verification because `$home` collides with reserved `$HOME`; renamed `$guideHomeDocument` and the new probes still require successful Windows qualification.

Historical blocked audit (2026-10-02): final audible confirmation, local Rumble credential entry/baseline and CPU-target disposition remain unresolved across at least three consecutive goal turns. Independent implementation, documentation, wiki publication and package checks are recorded above. Latest documentation commit `c3f8cb2` has active Windows run [37061856073](https://github.com/TechDaddyKB/tdsblive/actions/runs/37061856073); its SonarQube/packaging result is not yet verified. Leave the owned test host running and do not infer confirmation or acceptance from elapsed time. On resume, inspect that run and the live host, resolve these gates, then finish protected delivery and release publication.

Resumed qualification (2026-10-02): operator reports the Rumble baseline established. Windows run `37061856073` for `c3f8cb2` has completed successfully. Independent host verification currently finds no listener on port 17474; the operator was asked to refresh the editor before further audio tests. No credential file was read. Subsequent owned-host recovery restores editor HTTP 200, both bot connections and real Rumble healthy/baseline state. Two fresh owned support examples produce Speaker.bot dispatched/acknowledged and OBS sound completed/browser-playback-completed receipts; both rules are disabled afterward. The operator confirms “Heard both.” These additional examples affect only disposable totals; actual paid-platform delivery remains unverified. The enabled-Rumble measurement is complete: 60.01-second idle averages 4.616% CPU; 180.01-second ordinary chat averages 5.600% CPU with 360 synthetic preview messages (two/second); peak backend RSS is 321.35 MB (306.46 MiB). One logical core is 100%; separate all-32 normalization is 0.1443%/0.1750%, not used to replace that comparison. Both bots and two owned OBS browser sources are connected, Info logging is enabled, rules are disabled and there is no recurring qualification observer. The operator selected “Release with documented Wine limitations,” explicitly accepting CPU and memory deviations.

### Final delivery

Final delivery (2026-10-02): [Windows run 37073729890](https://github.com/TechDaddyKB/tdsblive/actions/runs/37073729890), source `649b2d105bc3bda0bfd1012adcc40bbf190ddef6`, passes 75 core and 384 host tests without skips, 175 frontend tests, OpenCover/LCOV imports, SonarQube quality gate and native ZIP/installer qualification. All protected PR checks pass. Both downloaded package checksums match, and the actual downloaded EXE passes both real-browser read-only bot tests under Wine with empty execution histories and graceful exit. Its bundled guide passes ten chapters/eight images, navigation and UTF-8 with networking disabled. Protected [PR #13](https://github.com/TechDaddyKB/tdsblive/pull/13) merged as `be467726b251fa31bb80f4d1eef1711e693b0870`; its tree exactly matches tested source `649b2d1`. [MVP release v0.1.0](https://github.com/TechDaddyKB/tdsblive/releases/tag/v0.1.0) is public, tagged at that merge and includes the verified Windows x64 ZIP, installer and `SHA256SUMS.txt`; uploaded digests and sizes match all three local artifacts. Wiki `3913754` publishes the matching complete guide and passes fresh-browser checks. The completion-record change is documentation only and follows normal protected delivery.

G00–G10 satisfy MVP completion under the explicit approved scope. G11 completion is recorded below; G12–G13 remain Not started. Wine performance deviations are accepted and documented, not passing targets. Actual paid-platform delivery and native Windows streaming-PC performance remain unverified; VTube Studio remains on hold.

### Blockers

None for the approved G10 scope. The operator accepts documented Wine CPU and memory deviations; native Windows streaming-PC performance and actual paid-platform delivery remain explicitly unverified by scope.

<a id="g11"></a>

## G11 — Complete advanced editor and built-in widgets

Status: **Complete**
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

2026-10-02: implementation adds persisted flat groups, selection movement/rotation/resize, independent grouped copy/paste, alignment/distribution, z-order, selection lock/hide, separate grid visibility and pan controls. Event List and Goal/Progress bars include backend validation, scoped history, bounded rendering, ledger totals and preview isolation. All typed field controls have renderer evidence. Requirement mapping and limits: [G11 advanced editor](g11-advanced-editor.md).

Local evidence so far: 182 frontend tests pass with 88.78% line coverage; 76 core and 382 host tests pass with four explicit Windows-only skips. Targeted advanced persistence/history tests pass. The real-browser audit identified a default-normalization autosave loop; the editor now accepts returned defaults only when no newer local edit exists, with a regression test. Follow-up local evidence: 184 frontend tests pass with 89.04% line coverage. Full fresh-browser G02/G05/G06/G07/G08/G09/G11 qualification passes, including repeated grouped transforms, independent copying, resize/pan/grid, Event List bounds/reconnect, both progress kinds and MVP media workflows. Real-process restart/restore passes grouped geometry, advanced settings and referenced-image restoration with an open reconnecting browser. The current OpenAPI snapshot passes actual-process drift/crash/restart qualification.

OBS 32.2.2 / WebSocket 5.7.4 displays the owned Event List, 12.5% ledger goal, 50% manual bar, Latest Supporter and silent VP9 video in a separate preview scene. Actual PNG alpha spans 0–1, and successive video crops differ. A live manual update to 80% and 15-degree rotation renders correctly. An actual host restart reconnects OBS and retains the subsequently saved 20% value. The owned backup then restores the 80% value, transformed geometry, Event List, ledger total, supporter and video in an actual OBS screenshot. The operator confirms the three short owned OBS tones were heard. Prior OBS scene/studio mode were restored, the owned scene/source removed and disposable host shut down. Exact-head Windows/Sonar evidence remains pending. First Windows run `37085354638` reached real-browser qualification but began editing before the newly created canvas was installed; the qualifier now waits for its saved identity/name before proceeding. Corrected local browser qualification passes. This checkpoint is not G11 completion.

### Blockers

None for the approved G11 scope. Corrected commit `cf00f6944e5d694e1c651379b16b51ed7911f2fe` passes [Windows CI run 37087437529](https://github.com/TechDaddyKB/tdsblive/actions/runs/37087437529), including native tests, rendered-editor qualification, actual-process restart/restore, contracts, SonarQube, self-contained ZIP/installer creation and native portable/installed EXE recovery, reinstall, startup opt-in and uninstall. SonarQube passes with 84.3% new-code coverage, zero new duplication and all security hotspots reviewed. The offline guide passes a network-disabled browser audit of all ten chapters and eight images. The runtime audit reports 78,777 compressed JavaScript bytes and no editor dependencies. Audible OBS qualification used the owned local program scene with streaming and recording off; prior OBS state was restored. All G11 acceptance criteria are satisfied. [PR #15](https://github.com/TechDaddyKB/tdsblive/pull/15) contains the implementation and operator-requested AGENTS/OBS-routing changes; final completion documentation follows the same protected delivery checks. G12 custom code and package portability remain out of scope.

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

Real OBS sound/video behavior, final media/reconnect/restore audit, Windows CI and SonarQube quality gate remain acceptance gates. Implementation and verification continue; these are not grounds to mark the goal complete.

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

Real OBS sound/video behavior, final media/reconnect/restore audit, Windows CI and SonarQube quality gate remain acceptance gates. Implementation and verification continue; these are not grounds to mark the goal complete.

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
