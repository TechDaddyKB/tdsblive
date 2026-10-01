# G07 financial ledger and supporter identities

Status: In progress. The full acceptance contract remains in [the implementation plan](implementation-plan.md#g07). Donor widgets belong to G08; financial automation belongs to G09.

## Amount and valuation contracts

`NativeMoney` carries integer native minor units, uppercase currency code, and explicit minor-unit digits. USD uses two digits; other source scales must come from verified currency/provider metadata. `FinancialPrecision` uses decimal rates and checked arithmetic, rounding to USD cents once per contribution, with midpoint rounding away from zero. Overflow rejects the valuation rather than truncating or saturating it.

`SupportValuator` gives documented native spend precedence over nominal rules. USD spend is `exact`. Other native spend is `fx` when a matching rate exists, retaining rate/date/provider and the estimated flag; absent rates yield `unknown` with `fx_unavailable`. Native spend is never silently replaced by a nominal amount. Contributions without documented spend use explicitly configured USD minor units per quantity, marked `configured_nominal` and estimated; without a rule they remain `unknown` with `nominal_unconfigured`. No default Bits/subscription prices are installed.

`ICurrencyRateProvider` takes currency and requested date. Rate direction is native major unit to USD major units. The provider implementation will use dated Frankfurter rates and preserve the actual observation date and attribution. The [official API](https://frankfurter.dev/) supports historical dates and provider filtering/attribution. Cache and dated manual overrides precede network lookups. Latest-rate fallback must be visibly estimated. Accepted values are stored and never change merely because the cache/provider changes; reconciliation is explicit and audited.

## Persistence and integration contract

- Durable unique source keys and canonical fallback keys, independent of chat retention. Transactional financial acceptance and restart catch-up must cover Streamer.bot and Rumble events even if their delivery outbox was already acknowledged.
- Normalize required Bits, donations, paid YouTube events, subscriptions/memberships, gifts, Ko-fi and Rants using isolated adapter strategies. Typed financial metadata must survive disabling raw capture. Document uncertain payloads rather than inventing amounts, giver identity, quantities or correlations.
- Correlate documented gift batch/individual identifiers transactionally so arrival order cannot duplicate total quantity. Unsupported/ambiguous gift accounting stays visibly gated, including Rumble gifts. Do not count recipient membership notifications as additional purchases.
- Store native spend, frozen valuation, source/event identifiers, occurrence time, quantity, supporter identity, stream identity, estimation/pending state and reconciliation audit. Index occurrence/supporter/platform queries.
- Persist nominal rules, provider cache and dated manual overrides. Expose explicit reconciliation of pending/selected records without silently recomputing accepted history.
- Assign platform-qualified identities; matching display names never merge people. Manual linking/unlinking through authenticated, CSRF-protected editor controls changes aggregate attribution without changing original contribution evidence.
- Provide current-stream, timezone-local today/Monday-week/month/year/all-time and bounded custom periods. Define half-open boundaries and test DST/non-UTC dates. Unknown/pending amounts remain visible and separate from valued totals.
- Live ingestion excludes simulation/replay regardless of persistence test flags. Developer previews do not write financial rows. Financial actions remain disabled by default in tests.
- Document HTTP APIs, migrations, settings, provider limits and compatibility. HTTP and authenticated LAN remain supported without an HTTPS requirement.

## Current validation

`dotnet test tests/unit/ExtensionSuite.Core.Tests -c Release --no-restore` passes all 36 tests, including three new financial tests covering decimal precision, scales, rounding, valuation precedence, attribution, pending/nominal labels, invalid input and overflow. End-to-end ledger ingestion, UI, correlation, FX provider and full G07 qualification are not yet implemented. Do not treat the pure valuation tests as proof of complete ingestion or accounting.

The persistence layer is implemented by `FinancialStore` and migration `20261001213840_FinancialLedger`. Platform/native-ID and platform/dedupe uniqueness survive fresh repository construction against the same SQLite file. Identity and contribution acceptance commit together; manual linking updates indexed attribution and records an audit in the same transaction. Repeated deliveries do not revalue accepted FX history. Typed `CanonicalEvent.Support` survives absence of raw capture. Gift accounting now uses the durable correlation strategy below; unsupported and ambiguous evidence remains gated.

Store-level tests verify actual SQLite persistence, rollback, identity linking, frozen historical values and exact period totals. Provider/cache/rules/reconciliation, typed adapters and the independently recovering projection are implemented below. Actual process restart qualification, editor APIs/UI, cache refresh/bulk reconciliation and Windows/SonarQube acceptance remain required.

## Period and total contracts

`LedgerPeriods` derives today, Monday week, month and year boundaries from the configured timezone, converting each boundary separately to UTC. Calendar days are not assumed to last 24 hours. Custom dates use `[start local date, end local date)`; all-time has no bounds. Current-stream requires an explicit persisted stream start and ends just after the query instant; no startup-time or inferred stream boundary is substituted. Settings persistence and stream-start UI still need integration.

`FinancialStore.TotalsAsync` applies occurrence indexes to half-open UTC filters and joins current supporter attribution. It streams integer values and sums with `BigInteger`, avoiding SQLite SUM overflow/floating-point fallback. The wire contract returns authoritative minor-unit totals as decimal integer strings, preserving values beyond JavaScript safe integers and signed 64-bit aggregate sums. Exact, FX and configured nominal totals are separate; unknown, estimated and gated counts remain visible. Excluded recipient notifications do not contribute. Result limits are 1–1000; ties sort deterministically by supporter ID. Public endpoints and editor views remain to implement.

All 40 core tests and six SQLite ledger tests pass locally, covering DST 23/25-hour days, Monday boundary, local month/year, custom and current-stream ranges, aggregate overflow safety, half-open filters and linked identities. This does not yet prove process recovery, adapter behavior or browser UI.

## FX provider and cache implementation

`FrankfurterRateProvider` requests the fixed HTTPS `/v2/rate/{currency}/usd?date={date}` endpoint with only currency/date. The source currency must be uppercase and the response must match currency, USD quote, a positive decimal rate and a valid observation date. Older historical observations are retained with their actual date; future observations cannot masquerade as historical rates. Attribution is `frankfurter:v2:blended`, not an invented individual central bank. Historical 404 permits a latest lookup, always marked estimated. HTTP 429/5xx/422, malformed data, mismatches, network errors and internal timeout return no rate rather than silently pricing support. Caller cancellation propagates. Each request has a ten-second bound and a 64 KiB body limit. Host registration is implemented; live application lookup still requires qualification.

`CachedCurrencyRates` persists observations by currency/requested day/origin. Manual dated overrides take precedence; provider cache writes use atomic first-observation-wins insertion. An override created during a network request is rechecked before returning. Overrides and their removal record an audit transactionally. A new cache instance reuses persisted rates; unavailable results are not stored as valid rates. Changing overrides does not touch accepted financial rows. Explicit reconciliation and provider-cache refresh controls remain required.

Fourteen FX tests pass for decimal/date preservation, latest fallback, temporary failures, invalid schemas/routing, size/transport/cancellation limits, persisted cache, audited dated override/removal, frozen ledger history and concurrent override precedence. A read-only live public endpoint probe on 2026-10-01 returned the dated EUR/USD response shape for 2026-01-05. This probe sent no supporter data and is not proof of integrated application lookup; that remains an acceptance check.

## Explicit reconciliation and nominal rules

`FinancialStore.ReconcileAsync` requires the stored version. It computes a proposed valuation and atomically compares/updates that version with an audit record. A stale request returns a conflict result; audit insertion failure rolls back both value and version. An unavailable new lookup does not erase an accepted amount. Gated or excluded purchase evidence cannot be bypassed through manual nominal reconciliation. Rate request dates must match the contribution’s UTC occurrence date; actual observation dates remain separate. Original amount, quantity, identity and source evidence are preserved.

`ValuationRuleStore` leaves all rules absent until configured. Keys include platform/type/tier; integer-minor-unit-per-quantity values are decimal strings. Creation uses expected version zero, subsequent edits use the stored version, and rule changes are audited. Zero is an explicit configured value, not an unconfigured default. Changing a rule alone never revalues history. Editor CRUD, provider cache refresh and bulk pending reconciliation remain unfinished; the independently recovering worker is registered.

The focused financial/FX test selection now passes 24 tests. New reconciliation cases verify pending-to-valued conversion, stale/no-op/failure preservation, explicit nominal choice, preserved historical totals after rule edits, inability to bypass gift gating and rollback when the audit write fails. No live production totals were changed.

## Typed adapter facts — implementation in progress

Support normalization now preserves quantity, tier, native money, reported amount/currency, gift role/correlation and recipient identity keys independently of optional raw capture. YouTube numeric micro amounts use the explicit ISO 4217 currency scale; conflicting values, unavailable scales and unrepresentable precision remain gated. The scale catalog comes from [SIX's current currency list](https://www.six-group.com/dam/download/financial-information/data-center/iso-currrency/lists/list-one.xml), published 2026-09-17 and retrieved 2026-10-01. Formatted display amounts are never parsed as money. Ko-fi exact money requires the documented forwarded trigger contract; its unpublished direct WebSocket shape remains gated. Missing Bits quantities remain unknown rather than becoming an invented purchase.

Rumble paid Rants retain typed USD cents. Gift recipients, channel scope and explicit subscription periods are retained for correlation independently of raw capture. Financial source fingerprints without native IDs use source timestamps and selected stable facts rather than receipt clocks. Anonymous support does not inherit a donor identity, and YouTube gift-receipt attribution uses the gifter rather than the recipient.

The adapter/financial/FX/Rumble replay selection passed 66 tests locally on 2026-10-01 before gift correlation was added. Public APIs, identity/settings editor controls and Windows/SonarQube qualification remain required; G07 is not complete.

## Durable ingestion projection

Migration `20261001222106_FinancialProjectionReceipts` adds event-keyed processed, unsupported and quarantined receipts. `FinancialProjection` reads bounded batches of persisted live support events independently of automation outbox acknowledgement, resolving dated rates and explicit nominal rules before ledger acceptance. The host registers the cached Frankfurter provider, rule store, ledger and independently restarted `financial-ledger` integration. Test/replay provenance is excluded. Legacy events without typed support are explicitly unsupported rather than reconstructed from optional raw capture.

A malformed typed event is quarantined with a fixed reason code; its private JSON and exception message are not copied into diagnostics. Storage errors leave the event eligible for retry. Ledger uniqueness protects the crash window between acceptance and receipt insertion, and retries of existing ledger entries bypass rate lookup so their accepted history remains frozen. Receipts do not imply successful automation delivery or proven gift purchase correlation.

Three actual SQLite tests verify acknowledged-outbox catch-up without raw capture, new-reader restart deduplication, malformed/legacy row isolation, replay exclusion and a forced receipt-write failure followed by recovery without repricing. The full host suite passed 230 tests with two Windows-only skips on 2026-10-01. Admin APIs/UI, live application FX qualification and Windows/SonarQube checks remain required.

## Durable gift accounting

Migration `20261001222806_GiftAccountingClaims` adds indexed source gift fields and durable purchase claims owned by ledger contributions. Claims, new contributions, structural exclusions and audit records commit together. Original sender keys are distinct from linked supporter IDs, so manual identity linking cannot change correlation evidence. Excluded rows retain their original valuation for history and cannot be reconciled into totals. Legacy rows receive role `none`; unavailable evidence remains gated rather than being invented during migration.

Twitch uses the documented relationship between an individual `communityGiftId` and the batch `id`. [Twitch's EventSub contract](https://dev.twitch.tv/docs/eventsub/eventsub-reference/) establishes that relationship; [Streamer.bot's gift schema](https://docs.streamer.bot/api/websocket/events/twitch/gift-sub) exposes it. Standalone gifts require explicit standalone origin and quantity one. Community individual notifications wait for the batch without increasing totals; the batch counts once, and matching notifications are excluded in either arrival order. Alternate batch notices cannot count twice. Conflicting tier, giver or quantity remains gated. Prime subscriptions use a distinct nominal-rule tier, `prime`, and explicit gift-recipient resub notices are excluded.

YouTube membership batches count once by channel/broadcast scope and source event ID. Gift membership receipt notices are excluded because they do not represent another purchase. These are configured nominal values only when the operator chooses a rule; no subscription retail prices are inferred.

Kick's published [individual](https://docs.streamer.bot/api/websocket/events/kick/gift-subscription) and [batch](https://docs.streamer.bot/api/websocket/events/kick/mass-gift-subscription) schemas have recipients and subscription start/expiry, without a documented batch ID. Claims therefore require exact channel scope, recipient platform IDs, sender platform ID and explicit UTC-normalizable start/expiry. Login/display names, receipt clocks, approximate time windows and anonymous sender guesses are not substitutes. Matching individual claims are transactionally replaced by the full batch, retaining historical values and an audit; a batch first excludes later matching individuals. Partial overlaps, conflicting facts and missing evidence remain visibly gated. Different subscription periods remain distinct. When the schema has no explicit channel field, scope is the sole connected Streamer.bot channel for that platform in this application database. Multiple independent channels require separate explicit scopes; this fallback does not prove cross-channel identity.

Twelve added checks cover both arrival orders, fresh readers, alternate batch notices, conflicts, partial overlap, period/source-ID requirements, missing evidence, Rumble gating, recipient exclusion and forced audit rollback. Two checks exercise real normalization → raw-disabled event persistence → independent projection → ledger, including reordered Kick recipients and changed reconnect receipt clocks. The full host suite passed 242 tests with two Windows-only skips on 2026-10-01. These are documented-contract and owned-fixture checks, not claims of live paid-event observation. Editor APIs/UI and remaining G07 qualification still need completion.

## Rate refresh and nominal-rule lifecycle

An explicit provider-cache refresh fetches a validated observation before opening its write transaction. Unavailable or mismatched responses preserve the cache. Replacement and audit are atomic; manual dated overrides retain precedence, and accepted ledger rows never change as a side effect. Cache listing exposes rate decimals as invariant strings with requested/observation dates, origin and estimated status.

Migration `20261001223937_ValuationRuleLifecycle` adds enabled state to existing nominal rules, defaulting existing rules to enabled. Removing a rule disables it with a versioned audit; future lookup returns unconfigured. Retaining the disabled row prevents stale pre-removal versions from changing a re-created rule. Reactivation requires the current version and leaves historical ledger amounts untouched. An explicit zero value remains different from a removed/unconfigured rule.

Five new SQLite maintenance tests verify removal/reactivation versions, historical-value preservation, manual precedence, refresh failure/mismatch and forced audit rollback. The focused maintenance/reconciliation/provider selection passed 22 tests; the complete host suite passed 247 tests with two Windows-only skips on 2026-10-01. Admin APIs/UI and explicit bulk reconciliation still require integration.
