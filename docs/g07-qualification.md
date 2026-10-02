# G07 completion audit

Status: In progress. Source under audit: `6134ddd5fa0326e2153f401742f1291e0a83e79e`. This audit covers [G07](implementation-plan.md#g07), specification sections 23–29 and 83, and the inherited security/testing contracts. G08 widgets and G09 financial automation are outside this goal.

## Requirement evidence

| Requirement | Evidence inspected | Result |
| --- | --- | --- |
| G04 prerequisite | [Merged PR #5](https://github.com/camarokris/tdsblive/pull/5), merge `47c122005b74c1a0ce957a9a80d8049f6dd4a26a`; verified ancestor of this branch | Verified |
| Bits, subscriptions, gifts, paid YouTube messages, memberships, Kick, Ko-fi and Rants | `SupportPayloadNormalizer`, observed Rant USD mapping in `RumbleSnapshotEngine`, `FinancialSourceAggregationTests`; all 11 required families combine after explicit linking | Verified with owned/documented contracts; paid live observation is not claimed |
| Additional donation adapter support | Typed `CanonicalEvent.Support` and the independent ledger projection accept platform-qualified support without needing raw captures; platform normalization strategies are isolated | Implemented extension contract; future providers require their own schema qualification |
| Quantity, username, occurrence time and Bits message | Typed support fields and immutable source facts in `FinancialStore`; combined-source test verifies retained message metadata | Verified |
| Integer native units and decimal USD valuation | `FinancialPrecisionTests`, native currency scale validation and exact-string editor tests, including aggregate totals beyond signed 64-bit sum range | Verified |
| Exact, FX, configured nominal and unknown distinction | `SupportValuator`, ledger/API fields and financial editor labels; explicit zero versus absent/removed rules | Verified |
| Unconfigured setup prices and transparent estimates | No installed Bits/subscription defaults; versioned rule store, rule lifecycle tests and real browser CRUD | Verified |
| Historical Frankfurter with observation date/provider | `CurrencyRateTests` and final Release `qualify_financial.py --configuration Release --live-fx` | Verified against actual provider through isolated host |
| Latest fallback is visibly estimated | Historical 404 fallback, date/provider validation and estimation tests; temporary failures stay pending | Verified with bounded owned transport tests |
| Persisted cache and dated manual overrides | Actual SQLite rate tests, concurrent manual precedence, audit rollback and real browser manual-rate controls | Verified |
| Frozen history and explicit reconciliation | Versioned per-contribution transactions, unavailable lookup preservation, stale conflicts, browser selected reconciliation and real crash/restart qualification | Verified |
| Durable uniqueness, reconnect/replay suppression | Ledger constraints, raw-disabled ingestion, financial projection receipts, actual restart/native-ID duplicate checks and source replay tests | Verified |
| Independent ingestion and failure recovery | Acknowledged outbox catch-up; storage failures retry; malformed/invalid/overflowing events quarantine without starving later batches | Verified with SQLite and actual process checks |
| Gift batches and individual notifications count once | Twitch/Kick arrival-order, exact recipient period, conflict and partial-overlap tests; YouTube recipient exclusion | Verified with owned documented-contract fixtures |
| Unverified gift ownership stays gated | Rumble gifts gated; unknown channel/recipient/period/sender facts remain gated; reconciliation cannot bypass accounting exclusions | Verified |
| Broadcaster channel context | Per-native-gift `GetBroadcaster` resolution; numeric/string/disconnected parsing tests; installed bot read-only field/type probe | Verified request/schema behavior; no paid gift observation claimed |
| Ko-fi uses Streamer.bot | [Forwarding contract](../integrations/streamerbot/kofi-forwarding.md); final Release `qualify_kofi.py --execute-synthetic-forwarding-action` executes installed CPH forwarding with owned simulation data | Verified real forwarding and ledger exclusion |
| Manual identities and no display-name merging | Transactional identity transfers/rollback; five separate platform identities in combined-source test; actual browser link/unlink and combined totals | Verified |
| Today, Monday week, month, year, all-time, custom and current stream | `LedgerPeriodTests`, indexed exact totals, timezone/API tests, persisted UTC stream start and real browser custom-period selection | Verified |
| DST and period precision | 23/25-hour days, separate local boundary conversion, half-open custom/stream filters and `BigInteger` aggregate tests | Verified |
| Public APIs and migrations | [G07 ledger contract](g07-financial-ledger.md), actual OpenAPI/generated types, versioned/CSRF endpoint tests, metadata preservation and real migration downgrade/re-upgrade | Verified locally; Windows generated-type check pending |
| HTTP and authenticated LAN admin boundary | Existing host middleware, scoped OBS token denial, antiforgery tests; financial UI uses HTTP and requires no HTTPS | Verified locally; Windows LAN runtime check pending |
| Test safety and public repository hygiene | Disposable qualifier directories, disabled automation/pre-acknowledged outbox, synthetic Ko-fi provenance, public tracked-file deterministic secrets scan and existing exclusions | Verified; no operator credentials or raw archive published |
| Windows build and runtime/browser qualification | [PR #8 checks](https://github.com/camarokris/tdsblive/pull/8/checks); previous run `36943667261` passed backend tests but failed an overbroad frontend text selector, now corrected to inspect the gated contribution's own cell | Pending corrected-head result |
| Backend OpenCover, frontend LCOV and SonarQube quality gate | Same trusted Windows workflow explicitly imports both and validates production coverage; local frontend LCOV is 93.60% lines/81.18% branches | Pending final-source analysis/coverage import and gate |
| Final protected delivery | [Draft PR #8](https://github.com/camarokris/tdsblive/pull/8); review ingestion rejection fixed in `6134ddd`, local backend suite 259 pass/2 Windows-only skips, frontend 102 pass | Pending final checks and merge |

## Fresh Release runtime checks

On 2026-10-01 the audited source built without warnings/errors. The following checks then passed using that Release host:

```text
python tools/qualify_financial.py --configuration Release --live-fx
npm --prefix tools/browser-qualification run qualify
python tools/qualify_kofi.py --execute-synthetic-forwarding-action
```

The browser qualifier uses fresh Chromium and disposable data; it does not control the operator's browser. The process qualifier crashes only its own child and sends only currency/date to Frankfurter. Ko-fi qualification executes only the imported allowlisted forwarding action with `isTest=true`; it makes no payment, changes no Speaker.bot queue and writes no production financial totals.

## Remaining work

Inspect final-source Windows results, actual OpenCover/LCOV import and SonarQube gate; resolve any findings; complete protected delivery and update the maintained G07 status. Unverified Rumble gift accounting stays gated by the acceptance contract. A green unit suite alone is insufficient to mark the goal complete.
