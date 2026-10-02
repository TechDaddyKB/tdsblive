# G10 release qualification

G10 remains **In progress**. This checklist records what is actually verified;
passing one row does not complete the other requirements. The owning contract is
[G10 in the implementation plan](implementation-plan.md#g10).

## Approved scope

- Qualify the available Wine/Proton environment and native Windows GitHub Actions.
- Native Windows streaming-PC performance is explicitly deferred by the operator.
- HTTP works locally and with authenticated LAN access; HTTPS is not required.
- VTube Studio work is on hold. Streamer.bot remains the production action authority.
- The tested Speaker.bot voice alias is `local english`.
- Use temporary application data and owned examples. Never broadcast, record,
  spend money or send public chat merely to manufacture qualification evidence.

## Acceptance checklist

| Requirement | Current evidence | Remaining evidence |
| --- | --- | --- |
| G00–G09 prerequisites | Maintained goal entries report completion; G09 protected merge is `26bb2eeea61ad5d4fbe6435f91778996de02d926` | Confirm final integrated behavior below; discovery alone is insufficient |
| Self-contained ZIP and per-user installer | Run `37041732834` / `da50bd5` passed native ZIP/installer, packaged restart/restore, startup off/opt-in, reinstall/uninstall and retained-data checks; downloaded package checksums match | Repeat exact-head checks after the Wine cryptography correction and publish the qualified release |
| Guided setup without normal-user commands | Local browser review/resume and configuration persistence pass; password entry defaults to session-only; import and action permissions are documented | Follow the guide against the final shipped package with local bots and OBS |
| SQLite-safe backup and restore | Archive/backend checks and local real-process recovery pass; Windows run `37031475607` passes managed-process restart/restore and cleanup | Final portable/installed EXE and Wine recovery; review actual safety-paused configuration and retained data |
| Repository, wiki and offline documentation | Ten chapters/eight owned screenshots; public wiki commit `48900b9`; all chapters and screenshots render in both public wiki and offline browser checks | Verify the guide included in the final shipped package and its setup instructions |
| Windows CI and SonarQube gate | Run `37041732834` / `da50bd5` completed successfully, including SonarQube, packaging and native package checks | Terminal success for the subsequent Wine compatibility correction |
| Rumble/chat/Rant/ledger/crown and Ko-fi/Bits together | Existing goal-specific evidence and full rebuilt local browser suite pass; Streamer.bot MCP confirms 1.0.7, three TDSBLive actions and thirteen triggers | Final shipped-package integration qualification, distinguishing live API/platform observations from owned replay/probe events |
| OBS rendering and audible output | Earlier operator observations are recorded in the owning G05/G06/G09 evidence | Final integrated OBS rendering/audio/reconnect checks; actual dispatch receipts alone are insufficient |
| Authenticated LAN HTTP | Existing Windows-specific authentication tests and authenticated owner-only recovery checks; editor LAN settings preserve unrelated configuration in browser checks | Final package authenticated remote HTTP behavior, credential rotation and restricted operations |
| Available-environment performance | Hardware identified below; no final candidate measurements yet | Idle below 1% CPU, ordinary-chat below 3% average CPU, backend below 300 MB, with workload, duration, CPU normalization and deviations recorded |

## Current local environment

Inspected on 2026-10-02:

- AMD Ryzen 9 7950X3D, 16 cores/32 logical processors, x86-64.
- Linux `7.2.5-3-omarchy`; Wine `11.17` Staging.
- OBS MCP is connected to the local Linux OBS instance; streaming and recording
  are inactive. This is distinct from running the Windows application under Wine.
- Streamer.bot 1.0.7 is reachable over WebSocket 8080 and HTTP 7474.
- Speaker.bot 0.1.7 is now listening on port 7580 after starting its normal
  launcher directly. Its GTK desktop launch attempt did not produce a running
  process. No voice or application settings were changed.

Do not normalize CPU usage by all 32 logical processors without saying so.
Report process CPU and memory separately from OBS/browser and Wine helper overhead.

## Latest checks

- All 170 frontend tests pass locally after media-test synchronization, with
  88.93% line coverage (LCOV generated).
- The rebuilt local Release host has zero warnings and errors.
- The full isolated browser suite passes chat, visual editor, financial ledger,
  donor widgets, automation simulation, setup persistence, real backup download
  and validation, and permission/LAN settings persistence. It enables no live
  integration merely by navigating setup or saving unchanged permissions.
- Local real-process restart and restore each create a new generation; restart
  retains later setup progress, restore recovers earlier progress, restored
  integrations/LAN are disabled, and quit stops the owned host.
- `python tools/qualify_bots.py --execute-local-test-trigger-and-queue --speaker-port 7580`
  passes against the current managed backend and actual installed bots: dedicated
  custom-trigger receipt, inspector observation, isolated replay, synthetic
  forwarder execution and Speaker.bot Pause/Resume. This does not establish
  final Windows-package behavior or audible output.
- `python tools/qualify_kofi.py --execute-synthetic-forwarding-action` passes
  installed CPH forwarding through `General.Custom` into typed USD support; the
  owned simulated contribution stays out of the financial ledger. This is a
  synthetic bridge check, not a real paid donation.
- Run `37030077956` passed recovery assertions but failed transient SQLite WAL
  cleanup on Windows. Bounded retries address that test cleanup race; the same
  recovery step passes in `37031475607`.
- The latest gate reported 82.7% new-code coverage. The offline-guide finding is
  resolved; the remaining executable-path findings prompted removal of that CLI
  argument entirely. Only `managed`, `portable` and `installed` modes are accepted;
  arbitrary paths are rejected before creating test data. The corrected final
  gate must still be observed, not inferred from these edits.
- Windows run `37033816613` evaluated `a65636a` and passed the security gate.
  Packaging failed while scanning guide sources without its authenticated CI
  environment. Packaging and native package checks now receive the same
  conditional scanner environment as existing trusted scan steps; forks receive
  no secrets. Native package contents are scanned before semantic inspection.
- Local Codex/VS Code MCP settings and standalone MCP configuration are excluded
  from public Git. The visual editor now offers manual URL copying when an HTTP
  browser omits or denies its clipboard API.
- Run `37037428506` passed analysis and authenticated guide-source scans, then
  failed reading UTF-8 Markdown with Windows' default legacy encoding. Guide
  reads now explicitly use UTF-8; final native packaging is still pending.
- Run `37040945969` passed backend tests but failed one frontend media assertion:
  the video element existed before React's playback effect applied its volume.
  The test now waits for the actual volume/mute/loop settings, including updated
  settings, while retaining its error and unmount cleanup assertions. A new
  exact-head Windows run is required; this failure did not reach packaging.
- Run `37041732834` completed successfully. Its scanned, checksum-verified
  Windows package starts under Wine, but CSRF token creation fails because the
  default ASP.NET protector opens a CNG SP800-108 provider unavailable here.
  A standalone owned Windows probe confirms DPAPI round-trip succeeds, default
  protection fails at that provider, and managed authenticated protection
  succeeds. The application now selects supported managed AES-256-CBC/HMAC-SHA256
  protection; five local host security tests pass, including payload tamper and
  purpose-isolation rejection. Actual corrected Wine and Windows CI checks are
  still required; no CSRF or credential protection is disabled.
- A locally published corrected Windows EXE passes actual Wine LAN HTTP
  authentication, credential rotation and DPAPI persistence across restart,
  scoped viewing-token authorization/revocation and owner-only remote 403 checks.
  Real restart/restore recovers earlier setup progress, pauses restored
  connections and retains the recorded safety directory. Actual bot connections,
  dedicated trigger receipt, isolated replay and CPH chat/Ko-fi forwarding pass;
  the owned simulated support remains excluded from the ledger. Final Actions
  artifacts and OBS/audio/performance qualification remain pending.
- Run `37044495358` / `ff330f2` completed successfully with the managed protector.
  Available-environment profiling of the local corrected EXE with connected bots
  and an OBS preview source measured 4.216% idle CPU over 60 seconds and 5.183%
  chat CPU over 180 seconds at two synthetic messages/second (one-core
  normalization); peak RSS was 288.32 MB. CPU targets were not met. A short
  Warning-logging comparison did not show a CPU improvement and does not justify
  suppressing diagnostics as a fix. Performance qualification remains open.
- An OBS-connected Wine restart exhausted the shutdown deadline, then crashed
  with `TaskCanceledException` in `DatabaseShutdown.StopAsync`. The operator's
  scanned `references/backtrace.txt` is consistent with that managed exception;
  it remains private/ignored. Shutdown now cancels browser subscriptions on
  `ApplicationStopping`, before Kestrel waits for connections. The expanded
  real-process qualifier keeps an overlay browser open and proves reconnect
  after restart and restore. Local managed qualification passes, and an actual
  locally published Windows EXE passes Wine recovery with the OBS source
  present; the captured console has no managed crash. A new exact-head CI run
  remains required.

## Completion rule

Keep pending rows pending until their required evidence exists. Record any
failed check and its correction; never replace final-package evidence with an
earlier candidate, a mock, an acknowledgment or a successful build. Only then
update G10 in the implementation plan and publish the qualified release.
