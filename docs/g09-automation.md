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

## Scheduler and persisted timer checks

The hosted dispatcher pumps independent named-group workers instead of waiting for the longest operation in a batch. It permits at most 16 concurrent groups, retains sequential dispatch within each group, and excludes occupied groups before applying the database batch limit. Shutdown waits for owned group tasks after cancellation. `DrainAsync` remains a bounded-batch helper for explicit test callers. Completed playback detail survives the final receipt transition.

Thirty-one targeted automation integration cases pass. New cases verify a newly arriving group can run past 110 queued actions behind a blocked group, active interruption precedes replacement dispatch, and completed receipts preserve adapter detail. Eight SQLite/controlled-clock cases cover Extend, Restart, Ignore and Queue across service recreation, with both toggle and separate-disable reversion, bounded queued repeats, exact deadlines and no repeated reversion after idle. These tests establish scheduler/persistence behavior, not actual VTube Studio state. Queued-effect receipt linkage and live interruption semantics still need qualification.

Draft [PR #12](https://github.com/TechDaddyKB/tdsblive/pull/12) publishes checkpoint `1e87a23`. Its initial [Windows CI run](https://github.com/TechDaddyKB/tdsblive/actions/runs/36975597976) reached the Sonar quality-gate step after build, tests and qualification succeeded; the gate was still pending at this evidence update. Subsequent scheduler/timer changes need their own head-specific Windows/Sonar verification.

## Queued-effect receipts and stopping rules

`AutomationPlannedAction.executionId` is additive and nullable for preview/legacy payloads. Persisted live plans carry the original receipt ID. Temporary queue admission saves queue membership and the receipt's `waiting-effect` state in one SQLite transaction, rejecting already requested cancellation. Later enable claims that same receipt before issuing a bot action; reversion updates it to completed only after acknowledgment, or uncertain when the result is ambiguous. Completion describes the recorded dispatch sequence, not independent proof of VTube Studio state. Legacy queued entries without a tracked ID become uncertain without issuing an enable action. Cancelled entries are skipped. Operator restoration clears the queue and cancels matching waiting receipts atomically.

Disabling or deleting a rule atomically cancels its queued/review/waiting work and requests interruption of active dispatch. Existing dispatched temporary effects retain their reversion schedule. Production accepted-event planning checks current enabled state/version inside queue admission, so a stale planner snapshot cannot authorize a disabled/deleted rule. Low-level store fixtures can explicitly omit that check; production `PlanAcceptedAsync` always requires it.

The latest full local suites passed 311 integration tests (four Windows-specific skips) and 139 frontend tests. Thirty-five targeted automation cases pass after the final transaction/cancellation checks, including stable queued enable/revert identity, ambiguous queued enable, cancelled/legacy work, restoration cleanup, disable/delete and stale planning rejection. Frontend type checking and regenerated API contracts pass. The initial Windows run ultimately failed its Sonar gate: new-code coverage was 75.5%, below 80%; reliability/security/maintainability ratings and duplication/hotspot conditions passed. Coverage and maintainability findings remain to address; no threshold/exclusion change is proposed.

## Sound WebSocket protocol and asset authorization

The backend selects one subscribed, live canvas socket for each sound. Preview sockets cannot receive automation sound commands or report completion. Another socket, even for the same authorized overlay, cannot complete the selected socket's pending command.

| Operation | Fields | Meaning |
|---|---|---|
| Server `sound` | `command.executionId` UUID, `assetId` SHA-256 ID, numeric `volume`/`duckingVolume` in 0–1, integer `timeoutSeconds` in 1–3600 | Play one local audio asset; ducking is metadata. |
| Client `sound-result` | `executionId`, `state`: started/completed/failed/timeout/interrupted | Started is not completion. Only the selected socket's terminal receipt settles the pending command. |
| Server `sound-received` | None | Receipt frame accepted; it does not prove playback. |
| Server `sound-stop` | `executionId` | Stop a cancelled or timed-out command without replaying it. |

Sound playback temporarily authorizes the chosen asset for the target overlay. Authenticated LAN fetches still require a valid scoped overlay token and `X-TDSBLive-Overlay`; the lease grants no automation/admin access or cross-overlay access. Completion, failure, timeout, cancellation or disconnect removes the lease. Token revocation interrupts the source. The browser fetches audio through that authorization, handles autoplay/media errors, releases object URLs/elements, bounds stalled fetches and suppresses duplicate command IDs. Chat-only and preview runtimes do not dispatch sounds or submit sound receipts. Reconnection/history is not permission to replay a sound.

Fourteen real backend WebSocket/HTTP cases pass, covering source ownership, preview isolation, scoped temporary assets, terminal states, timeout stop, cancellation stop, HTTP token revocation, malformed receipts and missing dependencies. Full local validation passed 325 integration tests (four Windows-only skips), 153 frontend tests, lint and type checking. Browser sound tests cover autoplay rejection, media errors, interruption, late fetch completion and invalid fields; socket tests cover preview/chat exclusion and closed-socket receipt suppression. These are protocol and browser-component checks, not captured OBS audio.

The Speaker.bot readiness check was corrected: this installation uses port **7580**, recorded in G03, rather than default 7680. An isolated temporary host connected to the actual Speaker.bot **0.1.7** on 7580 without speech or queue commands. A configured voice alias and real audible output remain unverified. The second [Windows run](https://github.com/TechDaddyKB/tdsblive/actions/runs/36977884740), at `b8d1b87`, passed build/tests/qualification but failed the Sonar gate at **77.0%** new-code coverage. The sound protocol cases above address the largest observed uncovered behavior; they need a new head-specific analysis.

The subsequent [Windows run 36979682918](https://github.com/TechDaddyKB/tdsblive/actions/runs/36979682918), at `5ec62fba322cfaaa2064089b5f08810dce860e8c`, completed successfully, including the Sonar quality gate. This evidence applies to that commit; later editor changes require their own CI analysis.

Sound rule configuration now selects canvas overlays by name and imported audio by filename. Multiple selected audio files become random variants. A fresh, isolated Chromium check against the Release host verified selection, save/reload persistence, repeated editing with optimistic version advancement, deletion, and safe simulation. Simulation produced neither execution receipts nor changes to the financial ledger. The reusable check is `tools/browser-qualification/automation.mjs`, included in the existing CI browser runner. All integrations were disabled and the WAV fixtures were generated locally. This does not qualify live OBS audio or Speaker.bot/VTube Studio behavior.

An additional real-process check used `tools/seed_automation_qualification.py` with its explicit live-test flag and a marked temporary G09 database. One owned Bits event was committed atomically with an acknowledged outbox row and a pending automation inbox row. The actual hosted worker dispatched a saved, enabled sound rule to a fresh Chromium canvas source. The source received exactly one sound command and its actual audio player produced a completed durable execution receipt. The rule was disabled afterward. This is live browser audio evidence, separate from simulation and mocked adapters; it is not OBS capture evidence. The helper never targets production data by default and refuses unmarked directories.

To repeat this local operator qualification, create a temporary directory named `tdsblive-g09-*`, start a host using that directory with external integrations disabled, and create the empty `g09-owned-qualification` marker. Import a generated/owned WAV, create a canvas and a sound rule matching Twitch `support.bits` with minimum quantity 100, and explicitly enable only that owned rule. Open its non-preview overlay URL in a fresh browser or OBS Browser Source; for OBS enable **Control audio via OBS**. After the source connects, run:

```sh
python tools/seed_automation_qualification.py /absolute/path/to/tdsblive-g09-owned-test --execute-owned-live-test
```

The helper refuses an absent database, an unmarked directory, or mismatched platform/type arguments. A local negative check verified refusal leaves no database file behind. An owned event is deliberately live within this isolated database; it may create isolated financial facts and actual audio. Never copy the marker into production data. Check the execution receipt, OBS meter/captured tone and reconnect behavior; then disable the owned rule. The helper is not a production endpoint and is never called by editor simulation or replay.
