# G09 automation rules

Status: In progress. Owning requirements: SPEC 32–36, 38, 61–62 and G09 in the implementation plan.

## Execution boundaries

Persist rules and event execution records independently of the financial ledger. Consume only accepted live events for normal execution; replay/simulation evaluates rules and reports intended actions without live side effects or financial writes. Rule disabling, filtering, moderation and failures never prevent financial ingestion.

Use stable rule/action IDs and event IDs for execution deduplication. Record dispatch intent before invoking an external operation. Distinguish confirmed dispatch, rejection, failure and uncertain outcomes; a successful Streamer.bot acknowledgment proves dispatch, not VTube Studio state. Never automatically repeat an operation that may have executed. Restart recovery must expose unresolved dispatches and temporary reversion state.

## Required behavior

- Exact, minimum, range and multiple-of conditions with explicit currency/quantity units; platform/event filters; multiple ordered actions; bounded named queues, cooldowns and execution history.
- Ko-fi TTS template fields, native minimum donation, voice alias, maximum length, username/amount/message controls, anonymous handling, URL removal, punctuation/repetition limits, bad-word controls, allowed languages and optional moderation. Viewer input remains untrusted.
- Bits overlay sounds use local asset-library audio, volume, queue/interrupt policy, cooldown, random variants and ducking metadata. OBS Browser Source performs playback; no system-default speaker dependency.
- Bits VTube Studio mappings select existing Streamer.bot actions by name/ID. Temporary effects use a second toggle or separate enable/disable actions with Extend, Restart, Ignore and Queue semantics. Preserve action selection allowlists and report missing actions.
- Local editor exposes rule CRUD, validation, discovery/capability limitations, moderation, execution inspection and safe simulation defaults. HTTP and authenticated LAN remain supported without mandatory HTTPS.

## Acceptance evidence to collect

Boundary and precision tests; persisted dedupe/restart behavior; queue overflow, cooldown, interruption and timer policy tests using controllable time; missing action/voice, disconnected and uncertain outcomes; moderation and simulation isolation; actual browser editing and overlay sound playback; real Speaker.bot speech through a synthetic owned Ko-fi event; real Streamer.bot-to-VTube Studio reversible action behavior; Windows CI, OpenCover/LCOV import and Sonar quality gate.

Live tests use temporary data and owned synthetic events. They require no paid donations or Bits purchase and do not alter production financial totals. VTube Studio availability has been requested from the operator. Live evidence remains outstanding.

## Current local evidence

The condition evaluator, speech preparation, rule contracts and temporary policies have 24 passing targeted core tests (`dotnet test tests/unit/ExtensionSuite.Core.Tests --filter FullyQualifiedName~Automation`). Conditions cover half-open ranges, exact/minimum/multiple boundaries, currency/scale matching and gated/recipient exclusions. Speech tests cover one-pass template expansion, URL removal, repetition/punctuation limits, anonymous suppression, Unicode-safe length, unknown-language review, moderation and Ko-fi message privacy.

Rule/action contracts and SQLite rule/execution stores use additive migrations, optimistic versions, unique event/rule/action receipts and restart recovery of dispatch intent to uncertain state. Accepted-live planning persists moderation/review decisions through a transactional inbox; preview and replay cannot enqueue. Dispatcher, persistent timers, browser sound commands and the editor are implemented but still need broader qualification. The latest targeted integration run passed 29 tests across automation and support normalization (`dotnet test tests/integration/ExtensionSuite.Host.Tests --filter 'FullyQualifiedName~Automation|FullyQualifiedName~SupportPayload'`), including SQLite reopen, isolation and normalized metadata serialization. This does not prove real speech, OBS sound playback or VTube Studio behavior.

## Ko-fi message metadata

The additive canonical `automation` object preserves nullable `anonymous`, `messagePublic` and `language` facts through event persistence. Live planning consumes those facts; it does not infer anonymity from visibility or infer a language from message text. Unknown anonymity retains conservative anonymous-message suppression. Ko-fi message text is eligible for speech only when `messagePublic` is explicitly true, regardless of the anonymous-message setting. Username/amount template controls remain independent.

Streamer.bot's [Ko-fi donation trigger documentation](https://docs.streamer.bot/api/triggers/integrations/ko-fi/donation) describes `isPublic` as message visibility; it does not provide an anonymity or language variable. The normalizer preserves explicit boolean `anonymous`/`isAnonymous` fields and a bounded language field when supplied, while unknown fields remain null. Supported forwarding and moderator input still need qualification; these metadata fields do not establish a native Ko-fi WebSocket schema. The safe simulator exposes separate public-message and anonymous-viewer controls and never persists or dispatches its result.

## Language and moderation review

Language-review receipts retain sanitized prepared speech for operator inspection and reserve bounded queue capacity like moderation-pending receipts. They participate in cooldown and interrupt cancellation. `POST /api/automation/executions/{id}/language` accepts the receipt version and an explicitly verified allowed language. It re-prepares the persisted live event, updates the receipt atomically and leaves it moderation-pending when the rule requires approval. Disallowed languages and stale versions cannot release the receipt. The moderation endpoint can reject language-review receipts but cannot approve them directly. The editor displays the prepared text and requires explicit language selection.

The latest automation-only SQLite/HTTP integration run passed 17 tests; core automation remained 24 passing tests. These checks include review capacity, allowed-language enforcement, stale-decision rejection and separate moderation before queue admission. They do not prove live external playback.

## Capability diagnostics and broader checks

`GET /api/automation/capabilities` performs read-only checks against saved rules, current connection state, action discovery/selection, canvas overlays and local audio assets. The editor exposes refreshable results. It identifies unavailable connections, missing/disabled/unselected actions, missing media and the voice-alias discovery limitation. It does not infer VTube Studio state or successful playback from cached discovery or acknowledgment.

On 2026-10-02 the full local suites passed 75 core, 293 integration and 138 frontend tests. Four Windows-specific integration cases were skipped locally. The subsequent capability endpoint tests passed two targeted HTTP cases without producing execution receipts. Frontend type checking, lint and four automation editor tests passed. Both production frontend bundles and the Release host built successfully with zero backend warnings/errors. `python tools/qualify_foundation.py` passed actual-process HTTP shells, API contract drift, crash/restart recovery, redaction and test isolation against the Release host.

Local process/socket inspection found Speaker.bot and Streamer.bot running, Streamer.bot listening on 8080, but no Speaker.bot listener on default port 7680 and no VTube Studio process/default-port 8001 listener. Operator setup information has been requested. These observations are readiness checks, not live G09 acceptance evidence.

## Temporary-effect uncertainty

Transport exceptions during enable or reversion immediately persist an uncertain effect; they do not authorize a repeat. Startup also recovers interrupted enable/revert intents as uncertain. `GET /api/automation/temporary-effects` exposes these records. The editor requires confirmation that the operator inspected and restored the external state before `POST /api/automation/temporary-effects/{id}/resolve` accepts the current version and `externalStateRestored: true`. Resolution records idle state and discards queued repetitions without sending any Streamer.bot action. Stale versions and absent confirmation are rejected. This recovery operation does not prove the external state; the operator's observation is required.

The subsequent full run passed 297 integration tests (four Windows-only skips) and 139 frontend tests. Four targeted temporary-store/action tests cover persisted expiry and ambiguous enable/revert behavior; an HTTP test verifies confirmation/version enforcement and zero bot dispatch during resolution. Five editor tests include restoration confirmation. Type checking and lint passed. All 68 pending source/documentation files passed deterministic secrets scanning before the checkpoint.

Remaining qualification includes scheduler fairness, interruption scope, timer stacking/restart and queued-effect execution tracking, scoped overlay sound security/browser protocol tests, actual editing/playback, live Ko-fi speech and VTube Studio actions, and Windows CI/Sonar coverage. The checkpoint is an implementation draft, not G09 completion.
