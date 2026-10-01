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

The initial persistence layer is implemented by `FinancialStore` and migration `20261001213840_FinancialLedger`. Platform/native-ID and platform/dedupe uniqueness survive fresh repository construction against the same SQLite file. Identity and contribution acceptance commit together; manual linking updates indexed attribution and records an audit in the same transaction. Repeated deliveries do not revalue accepted FX history. Typed `CanonicalEvent.Support` survives absence of raw capture. All gift accounting is temporarily gated until the documented correlation strategy is implemented; this is an unfinished implementation state, not a reduction of the required platform scope.

Five SQLite integration tests pass; the complete host suite passes 199 tests with two Windows-only skips. Actual process restart/backfill, adapter metadata, gift correlation, provider/cache/rules/reconciliation, editor APIs/UI and period queries still require implementation and acceptance evidence.

## Period and total contracts

`LedgerPeriods` derives today, Monday week, month and year boundaries from the configured timezone, converting each boundary separately to UTC. Calendar days are not assumed to last 24 hours. Custom dates use `[start local date, end local date)`; all-time has no bounds. Current-stream requires an explicit persisted stream start and ends just after the query instant; no startup-time or inferred stream boundary is substituted. Settings persistence and stream-start UI still need integration.

`FinancialStore.TotalsAsync` applies occurrence indexes to half-open UTC filters and joins current supporter attribution. It streams integer values and sums with `BigInteger`, avoiding SQLite SUM overflow/floating-point fallback. The wire contract returns authoritative minor-unit totals as decimal integer strings, preserving values beyond JavaScript safe integers and signed 64-bit aggregate sums. Exact, FX and configured nominal totals are separate; unknown, estimated and gated counts remain visible. Excluded recipient notifications do not contribute. Result limits are 1–1000; ties sort deterministically by supporter ID. Public endpoints and editor views remain to implement.

All 40 core tests and six SQLite ledger tests pass locally, covering DST 23/25-hour days, Monday boundary, local month/year, custom and current-stream ranges, aggregate overflow safety, half-open filters and linked identities. This does not yet prove process recovery, adapter behavior or browser UI.

## FX provider and cache implementation

`FrankfurterRateProvider` requests the fixed HTTPS `/v2/rate/{currency}/usd?date={date}` endpoint with only currency/date. The source currency must be uppercase and the response must match currency, USD quote, a positive decimal rate and a valid observation date. Older historical observations are retained with their actual date; future observations cannot masquerade as historical rates. Attribution is `frankfurter:v2:blended`, not an invented individual central bank. Historical 404 permits a latest lookup, always marked estimated. HTTP 429/5xx/422, malformed data, mismatches, network errors and internal timeout return no rate rather than silently pricing support. Caller cancellation propagates. Each request has a ten-second bound and a 64 KiB body limit. Host registration and live application lookup still require integration.

`CachedCurrencyRates` persists observations by currency/requested day/origin. Manual dated overrides take precedence; provider cache writes use atomic first-observation-wins insertion. An override created during a network request is rechecked before returning. Overrides and their removal record an audit transactionally. A new cache instance reuses persisted rates; unavailable results are not stored as valid rates. Changing overrides does not touch accepted financial rows. Explicit reconciliation and provider-cache refresh controls remain required.

Fourteen FX tests pass for decimal/date preservation, latest fallback, temporary failures, invalid schemas/routing, size/transport/cancellation limits, persisted cache, audited dated override/removal, frozen ledger history and concurrent override precedence. A read-only live public endpoint probe on 2026-10-01 returned the dated EUR/USD response shape for 2026-01-05. This probe sent no supporter data and is not proof of integrated application lookup; that remains an acceptance check.

## Explicit reconciliation and nominal rules

`FinancialStore.ReconcileAsync` requires the stored version. It computes a proposed valuation and atomically compares/updates that version with an audit record. A stale request returns a conflict result; audit insertion failure rolls back both value and version. An unavailable new lookup does not erase an accepted amount. Gated or excluded purchase evidence cannot be bypassed through manual nominal reconciliation. Rate request dates must match the contribution’s UTC occurrence date; actual observation dates remain separate. Original amount, quantity, identity and source evidence are preserved.

`ValuationRuleStore` leaves all rules absent until configured. Keys include platform/type/tier; integer-minor-unit-per-quantity values are decimal strings. Creation uses expected version zero, subsequent edits use the stored version, and rule changes are audited. Zero is an explicit configured value, not an unconfigured default. Changing a rule alone never revalues history. Editor CRUD, provider cache refresh, bulk pending reconciliation and worker integration remain unfinished.

The focused financial/FX test selection now passes 24 tests. New reconciliation cases verify pending-to-valued conversion, stale/no-op/failure preservation, explicit nominal choice, preserved historical totals after rule edits, inability to bypass gift gating and rollback when the audit write fails. No live production totals were changed.

## Typed adapter facts — implementation in progress

Support normalization now preserves quantity, tier, native money, reported amount/currency, gift role/correlation and recipient identity keys independently of optional raw capture. YouTube numeric micro amounts use the explicit ISO 4217 currency scale; conflicting values, unavailable scales and unrepresentable precision remain gated. The scale catalog comes from [SIX's current currency list](https://www.six-group.com/dam/download/financial-information/data-center/iso-currrency/lists/list-one.xml), published 2026-09-17 and retrieved 2026-10-01. Formatted display amounts are never parsed as money. Ko-fi exact money requires the documented forwarded trigger contract; its unpublished direct WebSocket shape remains gated. Missing Bits quantities remain unknown rather than becoming an invented purchase.

Rumble paid Rants retain typed USD cents. Gift recipients and batch fields are retained for the remaining correlation engine; retaining these fields does not prove purchase correlation. Financial source fingerprints without native IDs use source timestamps and selected stable facts rather than receipt clocks. Anonymous support does not inherit a donor identity, and YouTube gift-receipt attribution uses the gifter rather than the recipient.

The adapter/financial/FX/Rumble replay selection passed 66 tests locally on 2026-10-01. After the final anonymous/gifter attribution adjustment, the complete host suite passed 227 tests with two Windows-only skips. Worker integration, gift accounting, public APIs, identity/settings editor controls and Windows/SonarQube qualification remain required; G07 is not complete.
