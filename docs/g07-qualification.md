# G07 completion audit

Status: Complete. Qualified source: `29fce9658b44917ae9d6207c9507021b388fa70c`, merged through protected [PR #8](https://github.com/techdaddykb/tdsblive/pull/8) as `734cf7c8e972b7411f0b7a235081b348f3ad26fa`. This audit covers [G07](implementation-plan.md#g07), specification sections 23–29 and 83, and the inherited security/testing contracts. G08 widgets and G09 financial automation are outside this goal.

## Requirement evidence

| Requirement | Evidence inspected | Result |
| --- | --- | --- |
| G04 prerequisite | [Merged PR #5](https://github.com/techdaddykb/tdsblive/pull/5), merge `47c122005b74c1a0ce957a9a80d8049f6dd4a26a`; verified ancestor of this branch | Verified |
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
| Independent ingestion and failure recovery | Acknowledged outbox catch-up; storage failures retry; malformed/invalid/overflowing events, including an explicit null bridge path, quarantine without starving later batches | Verified with SQLite and actual process checks |
| Gift batches and individual notifications count once | Twitch/Kick arrival-order, exact recipient period, conflict and partial-overlap tests; YouTube recipient exclusion | Verified with owned documented-contract fixtures |
| Unverified gift ownership stays gated | Rumble gifts gated; unknown channel/recipient/period/sender facts remain gated; reconciliation cannot bypass accounting exclusions | Verified |
| Broadcaster channel context | Per-native-gift `GetBroadcaster` resolution; numeric/string/disconnected parsing tests; installed bot read-only field/type probe | Verified request/schema behavior; no paid gift observation claimed |
| Ko-fi uses Streamer.bot | [Forwarding contract](../integrations/streamerbot/kofi-forwarding.md); final Release `qualify_kofi.py --execute-synthetic-forwarding-action` executes installed CPH forwarding with owned simulation data | Verified real forwarding and ledger exclusion |
| Manual identities and no display-name merging | Transactional identity transfers/rollback; five separate platform identities in combined-source test; actual browser link/unlink and combined totals | Verified |
| Today, Monday week, month, year, all-time, custom and current stream | `LedgerPeriodTests`, indexed exact totals, timezone/API tests, persisted UTC stream start and real browser custom-period selection | Verified |
| DST and period precision | 23/25-hour days, separate local boundary conversion, half-open custom/stream filters and `BigInteger` aggregate tests | Verified |
| Public APIs and migrations | [G07 ledger contract](g07-financial-ledger.md), actual OpenAPI/generated types, versioned/CSRF endpoint tests, metadata preservation and real migration downgrade/re-upgrade | Verified locally and in trusted Windows CI |
| HTTP and authenticated LAN admin boundary | Existing host middleware, scoped OBS token denial, antiforgery tests; financial UI uses HTTP and requires no HTTPS | Verified locally and in the Windows runtime suite |
| Test safety and public repository hygiene | Disposable qualifier directories, disabled automation/pre-acknowledged outbox, synthetic Ko-fi provenance, public tracked-file deterministic secrets scan and existing exclusions | Verified; no operator credentials or raw archive published |
| Windows build and runtime/browser qualification | [Windows run](https://github.com/techdaddykb/tdsblive/actions/runs/36944795513); 40 core/261 host/102 frontend tests, runtime/browser/replay and generated-type checks pass | Verified |
| Backend OpenCover, frontend LCOV and SonarQube quality gate | [Trusted run](https://github.com/techdaddykb/tdsblive/actions/runs/36944795513) explicitly imports OpenCover and LCOV; [Sonar gate](https://sonarcloud.io/dashboard?id=camarokris_tdsblive&pullRequest=8) passes with 89.5% new-code coverage | Verified |
| Final protected delivery | [Merged PR #8](https://github.com/techdaddykb/tdsblive/pull/8), protected merge `734cf7c8e972b7411f0b7a235081b348f3ad26fa`; all required checks pass and the review finding is fixed | Verified |

## Fresh Release runtime checks

On 2026-10-01 the audited source built without warnings/errors. The following checks then passed using that Release host:

```text
python tools/qualify_financial.py --configuration Release --live-fx
npm --prefix tools/browser-qualification run qualify
python tools/qualify_kofi.py --execute-synthetic-forwarding-action
```

The browser qualifier uses fresh Chromium and disposable data; it does not control the operator's browser. The process qualifier crashes only its own child and sends only currency/date to Frankfurter. Ko-fi qualification executes only the imported allowlisted forwarding action with `isTest=true`; it makes no payment, changes no Speaker.bot queue and writes no production financial totals.

## Completion result

All G07 acceptance requirements are verified at the scopes identified above. The trusted Windows logs explicitly report LCOV analysis, four OpenCover report imports covering 68 production files, and QUALITY GATE STATUS: PASSED. Backend and frontend production coverage are 96.1% and 93.7% in the workflow verifier. The source is merged on main. No G07 blocker remains; unverified Rumble gift accounting stays gated by the acceptance contract, and paid live observations are not claimed.
