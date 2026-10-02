# G08 qualification audit

Status: In progress. Implementation: [PR #10](https://github.com/TechDaddyKB/tdsblive/pull/10). This audit follows SPEC sections 29–31, 84 and the Phase 9 widget list, plus the owning [G08 plan](implementation-plan.md#g08). It is not a completion claim.

| Requirement | Current evidence | State |
| --- | --- | --- |
| G05/G07 prerequisites | Main ancestry includes G05 PR #6 and G07 PR #8; G07 protected documentation delivery is the branch base `87efc67` | Verified |
| Five donor widget kinds | Validated persisted contracts, editor palette and properties, shared canvas renderer; actual Release-browser rendering for each kind | Verified locally |
| Ranked count 1–25 | Server/client validation, editor interaction test, 20,031-contribution SQLite test returns 25 rows | Verified locally |
| Period/platform/kind/minimum filters | Parameterized SQL filters before aggregation, half-open period tests including Chicago DST, editor controls, browser platform/minimum changes | Verified locally |
| Current stream start and totals | Persisted financial settings; unavailable-to-empty socket check, actual browser totals change after reconciliation; minimum applies to total | Verified locally |
| Exact values and linked identities | BigInteger SQLite aggregate and decimal-string wire/display amounts; beyond-64-bit qualification; live browser link/unlink and reconciliation | Verified locally |
| Unknown/gated/estimated states | Store/render tests, missing-stream-start socket test; unknown latest supporter retains its name without invented value, unverified gifts cannot win | Verified locally |
| Name/avatar/platform/amount/crown visibility | Editor/render tests; sanitized optional profile images and unavailable fallback; display properties do not alter accepted ledger facts | Verified locally |
| Templates/fonts/colors/images | Widget appearance controls, scoped image/font MIME/reference validation, asset/token lifecycle tests and asset loading hooks | Browser asset/appearance audit still required |
| Old-to-new leader transitions | Exit/enter animation and bounded timer tests; frequent snapshots cannot postpone transition indefinitely | Final built-browser/OBS check pending |
| Updates without refresh | Actual host sockets and Release browser prove ingestion, identity link/unlink, selected reconciliation, saved settings, revision restore and reconnect | Final revision qualification pending |
| SQL aggregation and bounded output | SQLite groups supporter/platform rows; exact custom sum avoids overflow; source facts are not loaded wholesale; 20,031-row query stays within ten seconds locally | Windows execution pending |
| Overlay authorization | Scoped-token donor assets and socket test; unrelated assets and financial administration return unauthorized; existing revocation boundary retained | Verified locally |
| Preview/test isolation | Simulation/replay exclusion, preview empty snapshots, actual browser preview does not expose production fixture totals, manual host disables integrations and pre-acknowledges outbox | Verified locally |
| Persistence/interfaces/compatibility | Donor settings participate in overlay revisions; old settings default; OpenAPI/types refreshed; no financial migration required | Final generated-type CI check pending |
| Actual OBS rendering | Prepared owned test host at port 17475 with all five widgets and alternating Alice/Bob leadership; browser rendering observed independently | Awaiting user confirmation |
| Windows CI and coverage | Earlier runs `36955866694` and `36956334158` failed at locked-file cleanup; lifecycle fixes are tested locally; run `36956960049` has passed .NET tests and continues qualification | In progress |
| SonarQube gate and renamed GitHub binding | Existing service keys retained, renamed public GitHub remote verified; analysis/decorated passing gate must be inspected | Pending |
| Protected delivery | Draft PR #10, no bypass or weakened checks | Pending |

Latest local scope-specific checks pass 18 donor integration tests. Broader local checks passed 51 core/269 host tests (two Windows-only skips) and 119 frontend tests before these additional period/performance tests. Counts identify their actual suite scope; Windows totals and final source must be recorded after the gate completes.

The user previously confirmed OBS alert video/audio; that satisfies G06 and does not substitute for G08 donor rendering. No paid live support is invented or required for qualification. Unverified Rumble gifts remain gated.
