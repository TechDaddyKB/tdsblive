# G10 release qualification

G10 is **Complete under the approved scope**, with the MVP release published. This checklist records what is actually verified;
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
- The operator selected “Use owned examples and explicitly leave actual
  paid-platform delivery unverified.” Reviewed Rant and owned Ko-fi/Bits examples
  qualify ingestion, totals and local automation in disposable data. They do not
  prove actual paid Rumble, Ko-fi or Twitch delivery, subscription/gift behavior,
  or effects on production totals. No test payment is required.

## Acceptance checklist

| Requirement | Current evidence | Remaining evidence |
| --- | --- | --- |
| G00–G09 prerequisites | Maintained entries and protected merges prove completion; integrated runtime evidence below qualifies their combined MVP behavior | None for approved scope |
| Self-contained ZIP and per-user installer | [Run 37073729890](https://github.com/TechDaddyKB/tdsblive/actions/runs/37073729890), source `649b2d1`, passes native ZIP/installer, startup off/opt-in, reinstall/uninstall and retained data; both package checksums and all uploaded asset digests/sizes match | None; [v0.1.0](https://github.com/TechDaddyKB/tdsblive/releases/tag/v0.1.0) is public |
| Guided setup without normal-user commands | Configuration/review persistence and session-only entry pass; actual final Actions EXE under Wine passes both real-browser bot tests without action/speech/queue receipts | None |
| SQLite-safe backup and restore | Actual Actions EXE under Wine passes restart, restore, paused consumers and extra restart; final Windows run passes browser-connected restart/restore and late-handshake regressions | None |
| Repository, wiki and offline documentation | Ten chapters/eight owned screenshots published on main and wiki `3913754`; actual final ZIP guide passes pages/images, navigation, UTF-8 and main plan link with networking disabled; preparation warnings removed | None |
| Windows CI and SonarQube gate | Final source `649b2d1`: 75 core/384 host/175 frontend tests, no backend skips, imported OpenCover/LCOV, SonarQube and all protected PR checks pass | Completion-record changes follow normal protected checks |
| Rumble/chat/Rant/ledger/crown and Ko-fi/Bits together | Actual shipped production libraries/workers deduplicate owned Rant/chat and project exact/nominal totals and widgets; real bot trigger/forwarder receipts pass. Current real Rumble API reports healthy/enabled/baseline established; operator confirms it | None for owned-example scope; actual paid-platform delivery remains unverified |
| OBS rendering and audible output | Actual source reconnect/rendering screenshot proves owned chat, crown/ranking and estimated totals. Fresh saved-rule receipts record OBS sound completion and Speaker.bot dispatch; operator confirms “Heard both” | None |
| Authenticated LAN HTTP | Actual Actions EXE under Wine passes remote HTTP sign-in, credential rotation, DPAPI persistence, scoped token revocation and owner-only 403 restrictions; restored to loopback | None; HTTPS is not required |
| Available-environment performance | Actual Actions EXE with real Rumble polling, both bots and OBS: 4.616% idle/5.600% chat CPU (one core = 100%) and peak RSS 321.35 MB. Complete workload and separate normalization are recorded below | Operator accepts documented Wine CPU/memory deviations. Targets are not claimed as passed; native Windows streaming-PC performance stays deferred |

## Final release evidence

Protected [PR #13](https://github.com/TechDaddyKB/tdsblive/pull/13) merged on 2026-10-02 at 22:59:22 UTC as `be467726b251fa31bb80f4d1eef1711e693b0870`. Its Git tree exactly matches tested source `649b2d105bc3bda0bfd1012adcc40bbf190ddef6`. [Release v0.1.0](https://github.com/TechDaddyKB/tdsblive/releases/tag/v0.1.0) was published at 23:02:03 UTC with the verified ZIP, installer and checksum file; the tag points to that merge. GitHub-reported SHA-256 digests and sizes match all three local artifacts. The final downloaded EXE passes the real-browser read-only bot checks and graceful exit under Wine, and its offline guide passes with networking disabled. No production payments, broadcast or recording were used. Completion-record updates are documentation only and follow protected checks.

Earlier checkpoints below retain their original failures/pending states as historical evidence. This current acceptance table and final release record supersede those pending statements.

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
- Run `37046606099` / `679f855` completes successfully. Scanned downloaded
  packages match both checksums; the actual ZIP's ten offline chapters and eight
  images render with navigation and correct UTF-8. Actual Actions EXE LAN HTTP
  qualification passes. The local corrected Windows publish also passes the
  combined real-browser visual/media, automation simulation, financial precision,
  identity linking, donor appearance/update and reconnect checks. OBS screenshots
  show four-platform chat after recovery, and PNG alpha inspection confirms a
  transparent background. These separate checks do not establish all remaining
  integrated/live or final-release acceptance gates.
- The Actions EXE restarts and restores with OBS open, then crashes on the next
  restart after bot configuration is restored. The last overlay request remains
  open for roughly 30 seconds. A connection can finish its handshake after the
  original shutdown snapshot, escaping that snapshot's cancellation. The hub now
  retains shutdown state, rejects new sockets with 503 and cancels handshakes
  that complete late. Six local socket/shutdown tests pass, including a
  deterministic delayed-handshake regression and existing concurrent-disconnect
  behavior. Corrected Wine and exact-head Windows checks remain required.

- Run `37049814743` / `d3a01e9541d35da5f78e0b5294b11f16a45191cc`
  completes successfully, including Windows tests, SonarQube, ZIP/installer and
  native recovery with a browser connected. Both downloaded package checksums
  match. The actual Actions EXE under Wine passes authenticated LAN checks,
  restart/restore and the previously failing additional restart with owned bot
  configuration. The exact package also passes the full real-browser visual/media,
  safe automation, financial precision, identity linking, donor appearance and
  reconnect suite. The late-handshake crash is corrected in this candidate.
- The operator approved owned paid-event examples with actual paid-platform
  delivery left unverified. A guarded private producer uses the actual shipped
  Core/Data/Rumble libraries against disposable application data. The reviewed
  synthetic Rant fixture yields one authoritative 125-cent Rant and one owned
  Rumble chat after baseline; reopening the store and repeating its snapshot
  accepts neither again. Packaged financial workers project exactly one $1.25
  Rant, one $5.00 Ko-fi example and 100 Bits at an explicitly configured nominal
  one cent per Bit: 725 USD cents total. Actual OBS screenshots show the Rumble
  chat once, a $1.25 crown before the Ko-fi example, then a $5.00 Ko-fi crown,
  three ranked supporters and $7.25 total with estimates identified.
- The two explicitly enabled owned rules produce exactly two execution receipts:
  Speaker.bot dispatch acknowledged using `local english`, and actual OBS browser
  sound playback completed. The rules were disabled afterward. Audible output
  still requires the operator's separate confirmation; these receipts alone are
  insufficient. These are owned examples, not actual paid-platform delivery.
- Inspecting the actual candidate's offline Home page exposed a guide-builder
  bug: external GitHub Markdown links were changed to `.html` as though they
  were local chapters. Only local chapter links are now rewritten. A real
  browser verifies ten corrected pages, eight images, local navigation, UTF-8
  and preservation of the remote implementation-plan URL. Native package checks
  now validate packaged local links and that external URL. Updated Actions
  packaging must still pass; the earlier candidate does not contain this fix.

## Final-candidate available-environment measurements

The actual `d3a01e9` Actions EXE was sampled with connected Streamer.bot and
Speaker.bot, two owned OBS browser sources, Info logging and three owned ledger
contributions. Both owned automation rules were disabled after their bounded
audio test. Rumble was disabled pending session-only credential entry; these
results do not measure enabled Rumble polling. Streaming and recording stayed
off. Chat traffic was 360 nonpersistent, side-effect-free synthetic text messages
over 180 seconds, two per second across four platforms, rendered in OBS preview.

| Process | Idle CPU, 60.02 s | Chat CPU, 180 s | Peak RSS |
| --- | --- | --- | --- |
| Windows backend under Wine | 4.449% | 5.383% | 297.14 MB / 283.38 MiB |
| Linux OBS, separately observed | 3.749% | 5.061% | 1483.78 MB |
| Owned Wine-prefix wineserver, separately observed | 1.083% | 1.389% | 16.88 MB |

All CPU values above use **one logical core as 100%**. Dividing backend CPU by
the machine's 32 logical processors gives 0.1390% idle and 0.1682% chat; those
are explicitly different normalizations and are not substituted for the failed
one-core targets. Backend RSS satisfies the 300 MB target in this workload.
Idle/chat CPU does not satisfy the respective 1%/3% targets using one-core
normalization. OBS and the wineserver are excluded from backend RSS; the table
does not sum every Wine helper or OBS child process. This measured deviation
remains visible, and native Windows streaming-PC performance remains deferred.

## Source-spec completion audit corrections

- Specification section 86 requires explicit integration connection tests. Setup
  now provides **Test Streamer.bot connection** and **Test Speaker.bot connection**.
  Each sends a correlated read-only `GetInfo` on the existing connection; it does
  not save form values, enable integrations, speak, change queues or run actions.
  Disabled, rejected and timed-out probes are reported truthfully. Older Speaker.bot
  metadata rejection does not disconnect an otherwise usable session. Rumble's
  **Connect Rumble** tests its actual URL and establishes the polling baseline.
- Seven targeted backend cases and eight setup UI cases pass. A real browser
  against an isolated managed host connected to the installed Streamer.bot 1.0.7
  and Speaker.bot 0.1.7 passes both new tests; their execution histories remain
  empty. The refreshed OpenAPI snapshot/generated types pass actual-process
  contract/recovery qualification. Full local suites pass 175 frontend tests
  (88.97% line coverage) and 380 host tests with four Windows-only skips. New
  probe behavior still requires Windows package qualification.
- Specification section 91's required documentation filenames are now present:
  architecture, installation, Rumble, events, overlays, widgets, automation,
  database, security and development. Seven added entry points link canonical
  plain-language guides and detailed contracts instead of duplicating instructions.
  README and all public Markdown local links resolve. Stale architecture/index
  statements are corrected against actual merged G08/G09 evidence.
- Wiki commit `e017419` publishes the read-only test instructions. The regenerated
  offline guide passes ten pages, eight images, navigation, UTF-8 and preserved
  external Markdown links in a real browser.
- Windows run `37054493247` / `86814ff` passed analysis and SonarQube, then failed
  native package checking because the new PowerShell `$home` variable collided
  with its reserved, read-only `$HOME`. It is renamed to `$guideHomeDocument`.
  The failed run is not counted as successful package qualification. The corrected
  verifier and new setup probes require a fresh successful Windows run.

## Verified final Windows package — 2026-10-02

- Windows Actions [run 37056770891](https://github.com/TechDaddyKB/tdsblive/actions/runs/37056770891), source `e10b8fb`, succeeds: 75 core tests, 384 host tests without skips, frontend checks, native ZIP/installer, restart/restore with an overlay open, startup opt-in/off, reinstall/uninstall and retained data. OpenCover and LCOV are imported; the SonarQube quality gate passes. All PR checks succeed.
- Downloaded ZIP and installer both match `SHA256SUMS.txt`. The scanned/extracted Actions EXE passes real-browser read-only connection tests against the installed bots under Wine with empty execution histories, followed by graceful exit and a closed listener. Its bundled guide passes ten chapters, eight images and navigation with browser networking disabled.
- The exact Actions EXE now serves port 17474 using preserved owned qualification data. An OBS screenshot after handoff confirms one owned Rumble message, three supporters and the $7.25 total with estimates marked. Previous d3 measurements included an owned helper polling status twice per second; that helper was retired before the new measurement.
- Actual `e10b8fb` Actions EXE, connected bots, two owned OBS sources, Info logging, disabled test rules and disabled Rumble: idle 60.00 seconds averages 4.000% CPU; ordinary chat 180.01 seconds with 360 synthetic preview messages (two/second) averages 5.144%, with one logical core as 100%. Peak backend RSS across both phases is 298.04 MB (284.23 MiB). Memory meets the 300 MB target; CPU exceeds the 1%/3% targets. Separate all-32-logical-processor normalization is 0.1250%/0.1608%, not a replacement for the one-core comparison. No related-process samples were taken in this run. Enabled Rumble polling and native Windows streaming-PC performance are not qualified.
- These successful checks supersede the fresh-Windows-package requirement following the historical `86814ff` reserved-variable failure. Audible confirmation, real API health, performance disposition and release publication remain open.

## Resume checkpoint — 2026-10-02

Final audio confirmation, real Rumble baseline and Wine CPU disposition remain unresolved across at least three consecutive goal turns. The owned host is running with both bots connected; Rumble has no session credential and remains disabled. Windows run [37061856073](https://github.com/TechDaddyKB/tdsblive/actions/runs/37061856073), commit `c3f8cb2`, is still active at the SonarQube step. Recheck its terminal result on resume. No merge, tag or release is published. Resolve the operator-dependent gates before protected delivery; do not substitute earlier audio evidence or dispatch receipts for the final audible check.

## Resumed operator checks — 2026-10-02

The operator reports “baseline established showing.” Preserve this as operator-observed evidence. A subsequent independent HTTP check found connection refused and no TDSBLive process on this machine; an editor refresh check is pending before audio qualification. Windows run `37061856073` for `c3f8cb2` completed successfully. This supersedes its historical active-run status above. Subsequent detached restart of the owned host restores editor HTTP 200. Independent status verifies both bots connected and Rumble healthy/enabled/baseline established, with its session credential present and forwarding disabled. The fresh audio qualification uses the actual e10b8fb production libraries and saved owned rules: one additional $5 Ko-fi example and 100 nominal one-cent Bits produce distinct receipts (speech dispatched/acknowledged-not-playback-confirmed; sound completed/browser-playback-completed). The operator confirms “Heard both,” supplying audible evidence alongside the receipts. Both owned rules are disabled afterward. These extra examples affect disposable test totals only. OBS streaming and recording were inactive. The operator selected “Release with documented Wine limitations,” accepting the measured CPU and memory deviations. Final enabled-Rumble measurement: 60.01-second idle at 4.616% CPU, 180.01-second chat at 5.600% CPU with 360 synthetic preview messages (two/second), peak backend RSS 321.35 MB (306.46 MiB). One logical core is 100%; separate all-32 normalization is 0.1443%/0.1750%. Both bots and two owned OBS sources are connected, Info logging is enabled, rules are disabled and no recurring qualification observer is active. Native Windows streaming-PC performance remains deferred. Final release-guide packaging and protected release delivery remain open.

## Final guide preparation

Wiki commit `3913754` publishes the final ten chapters/eight owned screenshots with the accepted Wine limitations. The generated offline guide passes all pages/images, navigation, UTF-8 and the main plan link with networking disabled. Final-source native packaging and protected release delivery still remain; no goal-completion claim is made from these documentation checks.

## Completion rule

Keep pending rows pending until their required evidence exists. Record any
failed check and its correction; never replace final-package evidence with an
earlier candidate, a mock, an acknowledgment or a successful build. Only then
update G10 in the implementation plan and publish the qualified release.
