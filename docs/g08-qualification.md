# G08 qualification audit

Status: Blocked on actual OBS donor confirmation. Implementation: [PR #10](https://github.com/TechDaddyKB/tdsblive/pull/10). This audit follows SPEC sections 29–31, 84 and the Phase 9 widget list, plus the owning [G08 plan](implementation-plan.md#g08). It is not a completion claim.

| Requirement | Current evidence | State |
| --- | --- | --- |
| G05/G07 prerequisites | Main ancestry includes G05 PR #6 and G07 PR #8; G07 protected documentation delivery is the branch base `87efc67` | Verified |
| Five donor widget kinds | Validated persisted contracts, editor palette and properties, shared canvas renderer; actual Release-browser rendering for each kind | Verified in Windows CI |
| Ranked count 1–25 | Server/client validation, editor interaction test, 20,031-contribution SQLite test returns 25 rows | Verified in Windows CI |
| Period/platform/kind/minimum filters | Parameterized SQL filters before aggregation, half-open period tests including Chicago DST, editor controls, browser platform/minimum changes | Verified in Windows CI |
| Current stream start and totals | Persisted financial settings; unavailable-to-empty socket check, actual browser totals change after reconciliation; minimum applies to total | Verified in Windows CI |
| Exact values and linked identities | BigInteger SQLite aggregate and decimal-string wire/display amounts; beyond-64-bit qualification; live browser link/unlink and reconciliation | Verified in Windows CI |
| Unknown/gated/estimated states | Store/render tests, missing-stream-start socket test; unknown latest supporter retains its name without invented value, unverified gifts cannot win | Verified in Windows CI |
| Name/avatar/platform/amount/crown visibility | Editor/render tests; sanitized optional profile images and unavailable fallback; display properties do not alter accepted ledger facts | Verified in Windows CI |
| Templates/fonts/colors/images | Widget appearance controls, scoped image/font MIME/reference validation, asset/token lifecycle tests and asset loading hooks; real browser uploads owned GIF/WOFF, verifies decoded image, loaded FontFace, template, color and size | Verified in Windows CI |
| Old-to-new leader transitions | Exit/enter animation and bounded timer tests; frequent snapshots cannot postpone transition indefinitely; built browser observes old/new fade classes after real link/unlink | Verified in Windows browser |
| Updates without refresh | Actual host sockets and Release browser prove ingestion, identity link/unlink, selected reconciliation, saved settings, revision restore and reconnect | Verified in Windows browser |
| SQL aggregation and bounded output | SQLite groups supporter/platform rows; exact custom sum avoids overflow; source facts are not loaded wholesale; 20,031-row query stays within ten seconds locally | Verified in Windows CI |
| Overlay authorization | Scoped-token donor assets and socket test; unrelated assets and financial administration return unauthorized; existing revocation boundary retained | Verified in Windows CI |
| Preview/test isolation | Simulation/replay exclusion, preview empty snapshots, actual browser preview does not expose production fixture totals, manual host disables integrations and pre-acknowledges outbox | Verified in Windows CI |
| Persistence/interfaces/compatibility | Donor settings participate in overlay revisions; old settings default; OpenAPI/types refreshed; no financial migration required | Verified in Windows CI |
| Actual OBS rendering | Prepared owned test host at port 17475 with all five widgets and alternating Alice/Bob leadership; browser rendering observed independently | Awaiting user confirmation |
| Windows CI and coverage | [Run 36960170546](https://github.com/TechDaddyKB/tdsblive/actions/runs/36960170546), source `a38ce7faefb39607359e0ae630a7082a552fcc58`, passes 51 core/281 host tests without skips, 121 frontend tests, replay/recovery/browser qualification, generated types and coverage import; production coverage is 96.2% backend/94.3% frontend | Verified |
| SonarQube gate and renamed GitHub binding | [PR quality gate](https://sonarcloud.io/dashboard?id=camarokris_tdsblive&pullRequest=10) passes at the exact source above: 88.5% new coverage, zero duplication, A reliability/security/maintainability, all hotspots reviewed. Analysis points to the renamed GitHub PR; GitHub SonarCloud check succeeds | Verified |
| Protected delivery | Draft PR #10, no bypass or weakened checks | Pending |

Final local checks at `b6ea8f2` pass 51 core tests, 277 host tests (two Windows-only skips), and 121 frontend tests, including the period/performance and editor loading regressions. The built browser checks also pass. The passing Windows source and authoritative totals are recorded above.

The user previously confirmed OBS alert video/audio; that satisfies G06 and does not substitute for G08 donor rendering. No paid live support is invented or required for qualification. Unverified Rumble gifts remain gated.

The S2077 finding `AaD6m93Q4KvHMX91J_XP` is addressed in source by removing query interpolation. Time bounds and JSON filter arrays are parameters in one fixed SQL statement. No rule exclusion or issue dismissal is used.

Run `36959527247` failed only at final deletion of the FinancialStore period test’s database; its assertions passed. Host shutdown and scoped pool clearing precede deletion. The test fixture now retries only Windows temporary-directory `IOException`s for at most two seconds; persistent locks still fail. Two real Windows handle tests verify transient recovery and persistent-lock failure. They are not claimed verified on Linux. Current local backend checks pass 51 core/277 host tests with four Windows-only skips. Run `36960170546` now verifies both native Windows cleanup cases and the static-query Sonar fix; no unresolved vulnerability remains.

Documentation commit `0c8a7644b8ce6d5f60be547b31a577fd38f50f84` also passes [Windows run 36961251786](https://github.com/TechDaddyKB/tdsblive/actions/runs/36961251786) and its exact-head Sonar gate. Native desktop automation is unavailable, and the required operator OBS confirmation remains unresolved across repeated goal turns. Implementation qualification is complete except for this actual OBS check; protected delivery follows it. The isolated fixture host remains available at `http://127.0.0.1:17475/overlay/donor-obs-check`, 1000 × 1000, with owned Alice/Bob updates and external integrations disabled.
