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

The condition evaluator and speech preparation have 18 passing targeted core tests (`dotnet test tests/unit/ExtensionSuite.Core.Tests/ExtensionSuite.Core.Tests.csproj --no-restore --filter FullyQualifiedName~Automation`). Conditions cover half-open ranges, exact/minimum/multiple boundaries, currency/scale matching and gated/recipient exclusions. Speech tests cover one-pass template expansion, URL removal, repetition/punctuation limits, anonymous suppression, Unicode-safe length, unknown-language review and moderation states. This does not establish durable execution, real speech, sound playback or VTube Studio behavior; those remain to implement and qualify.
