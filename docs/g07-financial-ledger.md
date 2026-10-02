# G07 financial ledger and supporter identities

Status: In progress. The full acceptance contract remains in [the implementation plan](implementation-plan.md#g07). Donor widgets belong to G08; financial automation belongs to G09.

## Amount and valuation contracts

`NativeMoney` carries integer native minor units, uppercase currency code, and explicit minor-unit digits. USD uses two digits; other source scales must come from verified currency/provider metadata. `FinancialPrecision` uses decimal rates and checked arithmetic, rounding to USD cents once per contribution, with midpoint rounding away from zero. Overflow rejects the valuation rather than truncating or saturating it.

`SupportValuator` gives documented native spend precedence over nominal rules. USD spend is `exact`. Other native spend is `fx` when a matching rate exists, retaining rate/date/provider and the estimated flag; absent rates yield `unknown` with `fx_unavailable`. Native spend is never silently replaced by a nominal amount. Contributions without documented spend use explicitly configured USD minor units per quantity, marked `configured_nominal` and estimated; without a rule they remain `unknown` with `nominal_unconfigured`. No default Bits/subscription prices are installed.

`ICurrencyRateProvider` takes currency and requested date. Rate direction is native major unit to USD major units. The provider uses dated Frankfurter rates and preserves the actual observation date and attribution. The [official API](https://frankfurter.dev/) supports historical dates and provider filtering/attribution. Cache and dated manual overrides precede network lookups. Latest-rate fallback must be visibly estimated. Accepted values are stored and never change merely because the cache/provider changes; reconciliation is explicit and audited.

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

The persistence layer is implemented by `FinancialStore` and migration `20261001213840_FinancialLedger`. Platform/native-ID and platform/dedupe uniqueness survive fresh repository construction against the same SQLite file. Identity and contribution acceptance commit together; manual linking updates indexed attribution and records an audit in the same transaction. Repeated deliveries do not revalue accepted FX history. Typed `CanonicalEvent.Support` survives absence of raw capture. Gift accounting now uses the durable correlation strategy below; unsupported and ambiguous evidence remains gated.

## Period and total contracts

`LedgerPeriods` derives today, Monday week, month and year boundaries from the configured timezone, converting each boundary separately to UTC. Calendar days are not assumed to last 24 hours. Custom dates use `[start local date, end local date)`; all-time has no bounds. Current-stream requires an explicit persisted stream start and ends just after the query instant; no startup-time or inferred stream boundary is substituted. Timezone and UTC stream-start settings are persisted and editable in the financial editor.

`FinancialStore.TotalsAsync` applies occurrence indexes to half-open UTC filters and joins current supporter attribution. It streams integer values and sums with `BigInteger`, avoiding SQLite SUM overflow/floating-point fallback. The wire contract returns authoritative minor-unit totals as decimal integer strings, preserving values beyond JavaScript safe integers and signed 64-bit aggregate sums. Exact, FX and configured nominal totals are separate; unknown, estimated and gated counts remain visible. Excluded recipient notifications do not contribute. Result limits are 1–1000; ties sort deterministically by supporter ID. The financial endpoints and editor expose these exact-string totals.

## FX provider and cache implementation

`FrankfurterRateProvider` requests the fixed HTTPS `/v2/rate/{currency}/usd?date={date}` endpoint with only currency/date. The source currency must be uppercase and the response must match currency, USD quote, a positive decimal rate and a valid observation date. Older historical observations are retained with their actual date; future observations cannot masquerade as historical rates. Attribution is `frankfurter:v2:blended`, not an invented individual central bank. Historical 404 permits a latest lookup, always marked estimated. HTTP 429/5xx/422, malformed data, mismatches, network errors and internal timeout return no rate rather than silently pricing support. Caller cancellation propagates. Each request has a ten-second bound and a 64 KiB body limit. Host registration and an actual fixed-provider lookup through an isolated application host are verified.

`CachedCurrencyRates` persists observations by currency/requested day/origin. Manual dated overrides take precedence; provider cache writes use atomic first-observation-wins insertion. An override created during a network request is rechecked before returning. Overrides and their removal record an audit transactionally. A new cache instance reuses persisted rates; unavailable results are not stored as valid rates. Changing overrides does not touch accepted financial rows. Selected reconciliation and provider-cache refresh controls are available in the editor.

## Explicit reconciliation and nominal rules

`FinancialStore.ReconcileAsync` requires the stored version. It computes a proposed valuation and atomically compares/updates that version with an audit record. A stale request returns a conflict result; audit insertion failure rolls back both value and version. An unavailable new lookup does not erase an accepted amount. Gated or excluded purchase evidence cannot be bypassed through manual nominal reconciliation. Rate request dates must match the contribution’s UTC occurrence date; actual observation dates remain separate. Original amount, quantity, identity and source evidence are preserved.

`ValuationRuleStore` leaves all rules absent until configured. Keys include platform/type/tier; integer-minor-unit-per-quantity values are decimal strings. Creation uses expected version zero, subsequent edits use the stored version, and rule changes are audited. Zero is an explicit configured value, not an unconfigured default. Changing a rule alone never revalues history. Editor CRUD, provider cache refresh and selected reconciliation are implemented; the independently recovering worker is registered.

## Typed adapter facts

Support normalization now preserves quantity, tier, native money, reported amount/currency, gift role/correlation and recipient identity keys independently of optional raw capture. YouTube numeric micro amounts use the explicit ISO 4217 currency scale; conflicting values, unavailable scales and unrepresentable precision remain gated. The scale catalog comes from [SIX's current currency list](https://www.six-group.com/dam/download/financial-information/data-center/iso-currrency/lists/list-one.xml), published 2026-09-17 and retrieved 2026-10-01. Formatted display amounts are never parsed as money. Ko-fi exact money requires the documented forwarded trigger contract; its unpublished direct WebSocket shape remains gated. Missing Bits quantities remain unknown rather than becoming an invented purchase.

Rumble paid Rants retain typed USD cents. Gift recipients, channel scope and explicit subscription periods are retained for correlation independently of raw capture. Financial source fingerprints without native IDs use source timestamps and selected stable facts rather than receipt clocks. Anonymous support does not inherit a donor identity, and YouTube gift-receipt attribution uses the gifter rather than the recipient.

## Durable ingestion projection

Migration `20261001222106_FinancialProjectionReceipts` adds event-keyed processed, unsupported and quarantined receipts. `FinancialProjection` reads bounded batches of persisted live support events independently of automation outbox acknowledgement, resolving dated rates and explicit nominal rules before ledger acceptance. The host registers the cached Frankfurter provider, rule store, ledger and independently restarted `financial-ledger` integration. Test/replay provenance is excluded. Legacy events without typed support are explicitly unsupported rather than reconstructed from optional raw capture.

A malformed typed event is quarantined with a fixed reason code; its private JSON and exception message are not copied into diagnostics. Storage errors leave the event eligible for retry. Ledger uniqueness protects the crash window between acceptance and receipt insertion, and retries of existing ledger entries bypass rate lookup so their accepted history remains frozen. Receipts do not imply successful automation delivery or proven gift purchase correlation.

## Durable gift accounting

Migration `20261001222806_GiftAccountingClaims` adds indexed source gift fields and durable purchase claims owned by ledger contributions. Claims, new contributions, structural exclusions and audit records commit together. Original sender keys are distinct from linked supporter IDs, so manual identity linking cannot change correlation evidence. Excluded rows retain their original valuation for history and cannot be reconciled into totals. Legacy rows receive role `none`; unavailable evidence remains gated rather than being invented during migration.

Twitch uses the documented relationship between an individual `communityGiftId` and the batch `id`. [Twitch's EventSub contract](https://dev.twitch.tv/docs/eventsub/eventsub-reference/) establishes that relationship; [Streamer.bot's gift schema](https://docs.streamer.bot/api/websocket/events/twitch/gift-sub) exposes it. Standalone gifts require explicit standalone origin and quantity one. Community individual notifications wait for the batch without increasing totals; the batch counts once, and matching notifications are excluded in either arrival order. Alternate batch notices cannot count twice. Conflicting tier, giver or quantity remains gated. Prime subscriptions use a distinct nominal-rule tier, `prime`, and explicit gift-recipient resub notices are excluded.

YouTube membership batches count once by channel/broadcast scope and source event ID. Gift membership receipt notices are excluded because they do not represent another purchase. These are configured nominal values only when the operator chooses a rule; no subscription retail prices are inferred.

Kick's published [individual](https://docs.streamer.bot/api/websocket/events/kick/gift-subscription) and [batch](https://docs.streamer.bot/api/websocket/events/kick/mass-gift-subscription) schemas have recipients and subscription start/expiry, without a documented batch ID. Claims therefore require exact channel scope, recipient platform IDs, sender platform ID and explicit UTC-normalizable start/expiry. Login/display names, receipt clocks, approximate time windows and anonymous sender guesses are not substitutes. Matching individual claims are transactionally replaced by the full batch, retaining historical values and an audit; a batch first excludes later matching individuals. Partial overlaps, conflicting facts and missing evidence remain visibly gated. Different subscription periods remain distinct. When a native gift has no explicit channel field, the connection now issues GetBroadcaster before normalization and attaches only the connected platform account ID. It resolves each gift separately instead of reusing an earlier profile discovery. Missing, disconnected or unsupported account context remains gated as gift_channel_unavailable; the generic connected-channel fallback has been removed. Forwarded gifts must supply their explicit broadcaster identity.

## Rate refresh and nominal-rule lifecycle

An explicit provider-cache refresh fetches a validated observation before opening its write transaction. Unavailable or mismatched responses preserve the cache. Replacement and audit are atomic; manual dated overrides retain precedence, and accepted ledger rows never change as a side effect. Cache listing exposes rate decimals as invariant strings with requested/observation dates, origin and estimated status.

Migration `20261001223937_ValuationRuleLifecycle` adds enabled state to existing nominal rules, defaulting existing rules to enabled. Removing a rule disables it with a versioned audit; future lookup returns unconfigured. Retaining the disabled row prevents stale pre-removal versions from changing a re-created rule. Reactivation requires the current version and leaves historical ledger amounts untouched. An explicit zero value remains different from a removed/unconfigured rule.

## Financial HTTP API — backend implemented

All routes below inherit the existing loopback/authenticated LAN admin boundary. An OBS overlay token cannot read financial data. Mutations require antiforgery protection. HTTP is supported; HTTPS is not required for the local UI. Amounts, quantities and rates exposed to JavaScript are invariant strings, avoiding loss of integer/decimal precision. Native scale, valuation method, estimated state, source attribution and accounting exclusions remain separate fields.

| Route under `/api/financial` | Contract |
| --- | --- |
| `GET /ledger` | Bounded page: offset 0–1,000,000, limit 1–500; optional platform and all/counted/excluded/gated/pending state. Pending means counted with no USD valuation. |
| `GET /totals` | today/week/month/year/all-time/current-stream/custom, configured timezone; custom `start` and `endExclusive` are local dates; current stream requires an explicitly persisted UTC start. Limit 1–1,000 supporters. |
| `GET /settings`, `PUT /settings` | Persist timezone and optional current-stream UTC start immediately; expected version is the submitted settings version. UTC is the initial explicit default. No application restart is required. |
| `GET /identities` | Optional search, bounded 1–1,000 identities. Returns original platform keys and current supporter attribution. |
| `POST /identities/{id}/link`, `POST /identities/{id}/unlink` | Require the expected current supporter ID; stale attribution returns 409. Link targets an existing supporter. Unlink creates a separate supporter. All historical contribution attribution changes with the identity, transactionally audited. |
| `GET /rules`, `PUT /rules`, `DELETE /rules` | Exact decimal-string USD minor units per quantity, platform/type/tier and expected version. Removed rules remain disabled/versioned; a configured zero stays distinct from unconfigured. |
| `GET /rates` | Bounded cached observations, with exact decimal string, dates, origin/provider and estimated flag. |
| `POST /rates/lookup`, `POST /rates/refresh` | Explicit currency/date lookup or provider observation refresh. Refresh leaves accepted history untouched. Unavailable lookup is reported as unavailable. |
| `PUT /rates/override`, `DELETE /rates/override` | Explicit dated manual override/removal; exact decimal string, positive rate, USD identity restriction and audit. |
| `POST /reconcile` | Explicitly select 1–100 distinct ledger IDs with expected versions. Per-entry outcomes: reconciled/unchanged/pending/conflict/gated/not_found/out_of_range. Gated and excluded purchases cannot be revalued. |
| `GET /diagnostics` | Processed/unsupported/quarantined receipts, remaining source events, pending FX and gated contributions. Private payloads are not included. |

Reconciliation is transactional per contribution. Cancellation can leave earlier selected entries successfully reconciled; refresh state before retrying. It never automatically reprices all history. Monetary input rejects non-finite values, exponent notation and locale separators. Financial settings live in the existing configuration table under a separate key and maintain an audit/version without changing integration settings.

Migration `20261001225426_FinancialMetadataBackfill` preserves typed tier/role/scope/original-sender facts from older metadata in the indexed fields. Invalid JSON and unknown shapes are left alone. It changes neither amounts, versions nor accounting gates, and does not invent purchase claims. Downgrade leaves these recovered facts intact rather than clearing legitimate newer evidence.

`python tools/qualify_financial.py --configuration Debug --live-fx` passed on 2026-10-01 against an isolated real host. It verified the actual Frankfurter lookup through the application, dated conversion with retained provider/date/estimate metadata, acknowledged-event catch-up, persisted settings, crash/restart integrity, frozen history after rule/override changes, native-ID replay dedupe, explicit reconciliation, stale-version conflicts and simulation isolation. No operator data or live automation was used. Default qualification uses a dated manual fixture rate and is added to Windows CI; it does not require third-party network availability. Editor controls and populated browser qualification pass; the final Windows/SonarQube gate remains pending.

## Current qualification: channel ownership and editor controls

The GetBroadcaster response parsing follows the official [WebSocket request contract](https://docs.streamer.bot/api/websocket/requests#getbroadcaster). A read-only request against the installed Streamer.bot on 2026-10-01 confirmed connected Twitch, YouTube and Kick platform dictionaries and their broadcaster ID fields. Only field names/types were retained; account identities and private values were not printed. Tests verify numeric/string IDs and rejection of disconnected account entries. Full host checks pass 256 tests with two Windows-only skips. The Release host build passes without warnings.

The isolated Chromium qualification now exercises actual financial editor settings, persisted timezone/stream start, precise fractional-minor nominal rules, dated manual FX overrides, reload, removal and custom-period selection via real HTTP/CSRF APIs. It passes alongside prior editor/chat qualifications with no JavaScript page errors. Populated owned-fixture checks also pass for unknown/zero separation, same-name cross-platform identity separation, frozen accepted history, selected reconciliation, manual linking with combined totals, and unlinking. The fixture seeder refuses paths outside disposable browser-test directories; integrations are disabled and outbox rows are pre-acknowledged.

`python tools/qualify_kofi.py --execute-synthetic-forwarding-action` passes against the installed imported G03 CPH action, with an isolated temporary host. It verifies the documented donation fields through General.Custom into typed USD support and confirms simulation exclusion from the ledger. This is real forwarding execution with owned test data, not a payment observation. [Ko-fi forwarding setup](../integrations/streamerbot/kofi-forwarding.md) documents the allowlist, native event IDs, amounts/currencies, source timestamp and test isolation. Final Windows CI/SonarQube checks, requirement audit and delivery remain unfinished. G07 remains In progress.

The rule-lifecycle downgrade deletes disabled tombstones before removing the Enabled column, keeping removed rules absent in older schemas. This does not erase accepted ledger amounts or audit history. Downgrade/re-upgrade is covered by an actual SQLite migration test.


## Current qualification additions

`FinancialSourceAggregationTests` verifies all 11 required source families together through typed normalization (and the previously observed Rant USD contract), raw-disabled persistence, independent projection, explicit cross-platform linking and exact/nominal aggregation. Same display names remain five distinct platform identities before linking. This is owned-fixture contract qualification; Rumble engine/replay evidence remains in G04 and paid-event observations are not invented.

Frontend tests additionally verify exact fractional-minor rule edits, zero versus removed rules, rate/date/estimate disclosure, UTC settings normalization, manual identity selection, stale selected versions across live polling, gated selection denial and privacy-preserving errors. The complete frontend suite passes 102 tests; LCOV reports 93.60% lines and 81.18% branches.

A deterministic ledger validation/valuation rejection now receives a quarantined receipt with the fixed reason ledger_rejected. Actual SQLite tests place oversized identities, overflowing valuations and a malformed explicit-null bridge path before 32 valid later contributions; both bounded batches continue and commit those valid contributions. Canonical validation rejects a null bridge path as invalid_typed_event, rather than allowing a null-reference failure to stall the reader. Storage/receipt failures remain retryable and accepted history stays frozen.

Draft delivery is [PR #8](https://github.com/camarokris/tdsblive/pull/8). Final trusted Windows/coverage/SonarQube evidence and the completion audit remain pending.
