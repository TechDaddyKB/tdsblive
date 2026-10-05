# Tray release documentation audit

This records the preparation and completed documentation audit for G24. G22 and
G23 are accepted; 1.0.1 is published. Earlier checkpoints below retain their
historical context. The final delivery section supersedes publication-pending
statements; keyword discovery alone is not an audit pass.

Baseline: **76 tracked Markdown files**, inventoried from source `5b92f07` after
deterministic secrets scanning. The guide has 20 chapters and 12 existing images.
New chapters, screenshots, offline output and wiki navigation must be included
in the final audit too. Historical goal records retain their original versions
and evidence; current guidance must link the new release instead of replacing history.

Preparation adds **Tray-and-Desktop-Controls** to the shared navigation: the guide
now contains **21 chapters and 15 images**. Three new Windows screenshots come
from the scanned, visually inspected owned native run of `199f79f`; menu labels,
confirmation wording and fallback layout are unchanged by the later registration
fix. The page clearly identifies candidate/publication and Linux qualification
limits. An initial network-disabled browser check passes all 21 chapters, local
links/images, tray navigation and mobile/desktop reflow. This is preparation
evidence; final packaged/offline and public wiki checks still remain.

Windows lifecycle preparation updates installation, everyday use, updates,
troubleshooting and recovery to explain **Open editor**, quiet sign-in,
save-before-restart/quit, Cancel-first confirmations and missing-tray recovery.
Before-you-begin now explicitly distinguishes the public v0.1.0 download from
the unpublished cumulative v1.0.1 candidate. These are reviewed candidate
instructions; final Windows/Linux packages and publication must still verify
them before any row is marked complete.

The refreshed offline guide passes network-disabled checks of all **21 chapters**,
their local links and images, and every chapter at **320, 390 and 1366 pixels**.
Expanding the check beyond the tray page exposed unwrapped local viewing URLs in
the chat chapter; offline links now wrap without changing their addresses.

README preparation adds the tray start/open/quit flow, confirmation and fallback
guidance, and save-before-update instructions. It distinguishes public v0.1.0
from the unpublished v1.0.1 candidate. Linux companion downloads and final
release wording remain pending; the README row is not yet complete.

Technical-guide preparation now describes the implemented Windows companion,
authenticated pipe/loopback authority, profile ownership, graceful lifecycle,
native test boundaries and candidate installation flow in architecture, security,
testing and installation. The requirements matrix adds TR-R01–TR-R10 with current
Windows evidence and explicit Linux/publication gaps. A structural check of all
**78 tracked Markdown files** finds **454 local file links** with existing targets.
That check does not validate remote links, heading anchors, literal UI behavior
or final-release wording; it does not complete the page-by-page acceptance audit.

| File | Review or update required | Final evidence |
| --- | --- | --- |
| [docs/tray-documentation-audit.md](tray-documentation-audit.md) | Include new pages/assets and final package/wiki evidence | Complete; final delivery below records public/package/wiki evidence |
| [docs/user-guide/Tray-and-Desktop-Controls.md](user-guide/Tray-and-Desktop-Controls.md) | Prepared illustrated Windows controls; Linux instructions and release status still require completion | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [AGENTS.md](../AGENTS.md) | Retain workspace rules; verify release execution follows them | Reviewed; original contracts/provenance retained; local links checked |
| [CONTRIBUTING.md](../CONTRIBUTING.md) | Review unchanged workflow, labels and links; retain applicable guidance | Reviewed; original contracts/provenance retained; local links checked |
| [README.md](../README.md) | Download table, tray-first start/quit, Linux companion, published version | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [SPEC.md](../SPEC.md) | Add desktop and release requirements without rewriting original numbered requirements | Reviewed; original contracts/provenance retained; local links checked |
| [docs/architecture.md](architecture.md) | Native companion, control ownership and external Linux runner boundaries | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/automation.md](automation.md) | Review unchanged workflow, labels and links; retain applicable guidance | Reviewed; original contracts/provenance retained; local links checked |
| [docs/database.md](database.md) | Review unchanged workflow, labels and links; retain applicable guidance | Reviewed; original contracts/provenance retained; local links checked |
| [docs/desktop-lifecycle.md](desktop-lifecycle.md) | Keep candidate boundaries current; Linux lifecycle after implementation | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/development.md](development.md) | Current goals and desktop build/qualification links | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/events.md](events.md) | Review unchanged workflow, labels and links; retain applicable guidance | Reviewed; original contracts/provenance retained; local links checked |
| [docs/foundation-api.md](foundation-api.md) | Desktop diagnostics and retained HTTP authority | Reviewed; original contracts/provenance retained; local links checked |
| [docs/g00-validation.md](g00-validation.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/g01-validation.md](g01-validation.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/g02-validation.md](g02-validation.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/g03-integrations.md](g03-integrations.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/g04-rumble-ingestion.md](g04-rumble-ingestion.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/g05-overlays-chat.md](g05-overlays-chat.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/g06-editor-alerts.md](g06-editor-alerts.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/g07-financial-ledger.md](g07-financial-ledger.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/g07-qualification.md](g07-qualification.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/g08-donor-widgets.md](g08-donor-widgets.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/g08-qualification.md](g08-qualification.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/g09-automation.md](g09-automation.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/g09-qualification.md](g09-qualification.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/g10-qualification.md](g10-qualification.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/g11-advanced-editor.md](g11-advanced-editor.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/g12-custom-widgets.md](g12-custom-widgets.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/g13-compatibility.md](g13-compatibility.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/implementation-plan.md](implementation-plan.md) | Goal status and acceptance evidence | Complete; final delivery below records public/package/wiki evidence |
| [docs/installation.md](installation.md) | Current release evidence and tray/companion installation links | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/licenses/SimpleIcons-CC0.md](licenses/SimpleIcons-CC0.md) | Preserve fixture/license provenance; verify no tray-related instructions need changes | Reviewed; original contracts/provenance retained; local links checked |
| [docs/obs-mcp.md](obs-mcp.md) | Review unchanged workflow, labels and links; retain applicable guidance | Reviewed; original contracts/provenance retained; local links checked |
| [docs/overlays.md](overlays.md) | Review unchanged workflow, labels and links; retain applicable guidance | Reviewed; original contracts/provenance retained; local links checked |
| [docs/platform-logos.md](platform-logos.md) | Review unchanged workflow, labels and links; retain applicable guidance | Reviewed; original contracts/provenance retained; local links checked |
| [docs/releases/1.0.0.md](releases/1.0.0.md) | Retain as unpublished build history; link cumulative 1.0.1 notes | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/requirements-matrix.md](requirements-matrix.md) | Add TR requirements and exact current acceptance evidence | Complete; final delivery below records public/package/wiki evidence |
| [docs/rumble-analysis.md](rumble-analysis.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/rumble.md](rumble.md) | Review unchanged workflow, labels and links; retain applicable guidance | Reviewed; original contracts/provenance retained; local links checked |
| [docs/security.md](security.md) | Authenticated desktop pipes and Linux launcher settings | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/testing.md](testing.md) | Native desktop, runner, package and publication qualification | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/tray-release-plan.md](tray-release-plan.md) | Exact current evidence and remaining gates | Complete; final delivery below records public/package/wiki evidence |
| [docs/ui-manual-qualification.md](ui-manual-qualification.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/ui-parity-checklist.md](ui-parity-checklist.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/ui-redesign-completion-audit.md](ui-redesign-completion-audit.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/ui-redesign-plan.md](ui-redesign-plan.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/ui-redesign-qualification.md](ui-redesign-qualification.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/ui-usability-test.md](ui-usability-test.md) | Preserve historical requirements/evidence; verify current navigation and version context | Reviewed; original contracts/provenance retained; local links checked |
| [docs/user-guide/Adaptive-Editor-and-Guided-Alerts.md](user-guide/Adaptive-Editor-and-Guided-Alerts.md) | Review unchanged workflow, labels and links; retain applicable guidance | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/user-guide/Advanced-Editor-and-Widgets.md](user-guide/Advanced-Editor-and-Widgets.md) | Review unchanged workflow, labels and links; retain applicable guidance | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/user-guide/Automation.md](user-guide/Automation.md) | Review unchanged workflow, labels and links; retain applicable guidance | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/user-guide/Backup-and-Recovery.md](user-guide/Backup-and-Recovery.md) | Prepared save-before-tray-action and Cancel-first recovery guidance; final Linux/package review remains | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/user-guide/Before-You-Begin.md](user-guide/Before-You-Begin.md) | Prepared explicit public v0.1.0 versus unpublished v1.0.1 guidance; update after publication | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/user-guide/Chat-and-OBS.md](user-guide/Chat-and-OBS.md) | Review unchanged workflow, labels and links; retain applicable guidance | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/user-guide/Custom-Widgets-and-Portable-Packages.md](user-guide/Custom-Widgets-and-Portable-Packages.md) | Review unchanged workflow, labels and links; retain applicable guidance | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/user-guide/Everyday-Use.md](user-guide/Everyday-Use.md) | Prepared manual/quiet launch, save and end-of-stream tray quit; final Linux/package review remains | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/user-guide/First-Setup.md](user-guide/First-Setup.md) | Review unchanged workflow, labels and links; retain applicable guidance | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/user-guide/Glossary.md](user-guide/Glossary.md) | Prepared plain-language tray, companion and sign-in definitions | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/user-guide/Home.md](user-guide/Home.md) | Tray guide navigation, downloads and exact qualification limits | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/user-guide/Install-and-Update.md](user-guide/Install-and-Update.md) | Prepared tray quit before file replacement and fallback close behavior; four assets/native launcher remain | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/user-guide/Install-on-Linux-Proton.md](user-guide/Install-on-Linux-Proton.md) | Guided native UMU setup, installed runner/prefix retention and native browser; offline render verified; publication review remains | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/user-guide/Install-on-Linux-Wine.md](user-guide/Install-on-Linux-Wine.md) | Guided native Wine setup, retained prefix, manual/Bottles workflows and packaged setup screenshot; offline render verified; publication review remains | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/user-guide/Install-on-Windows.md](user-guide/Install-on-Windows.md) | Prepared illustrated automatic editor, hidden tray icons, quiet startup and quit; final native/package review remains | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/user-guide/LAN-Access.md](user-guide/LAN-Access.md) | Review unchanged workflow, labels and links; retain applicable guidance | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/user-guide/Local-StreamElements-Compatibility.md](user-guide/Local-StreamElements-Compatibility.md) | Review unchanged workflow, labels and links; retain applicable guidance | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/user-guide/Overlays-and-Alerts.md](user-guide/Overlays-and-Alerts.md) | Review unchanged workflow, labels and links; retain applicable guidance | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/user-guide/Supporter-Totals.md](user-guide/Supporter-Totals.md) | Review unchanged workflow, labels and links; retain applicable guidance | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/user-guide/Troubleshooting.md](user-guide/Troubleshooting.md) | Prepared missing Windows tray, stopped companion/backend and browser recovery guidance; native Linux remains | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [docs/widgets.md](widgets.md) | Review unchanged workflow, labels and links; retain applicable guidance | Reviewed; original contracts/provenance retained; local links checked |
| [integrations/streamerbot/kofi-forwarding.md](../integrations/streamerbot/kofi-forwarding.md) | Review unchanged workflow, labels and links; retain applicable guidance | Reviewed; original contracts/provenance retained; local links checked |
| [streamerbot/bootstrap/README.md](../streamerbot/bootstrap/README.md) | Review unchanged workflow, labels and links; retain applicable guidance | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [streamerbot/examples/README.md](../streamerbot/examples/README.md) | Review unchanged workflow, labels and links; retain applicable guidance | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [streamerbot/import/README.md](../streamerbot/import/README.md) | Review unchanged workflow, labels and links; retain applicable guidance | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [tests/fixtures/donors/README.md](../tests/fixtures/donors/README.md) | Preserve fixture/license provenance; verify no tray-related instructions need changes | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [tests/fixtures/media/README.md](../tests/fixtures/media/README.md) | Preserve fixture/license provenance; verify no tray-related instructions need changes | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [tests/fixtures/rumble/README.md](../tests/fixtures/rumble/README.md) | Preserve fixture/license provenance; verify no tray-related instructions need changes | Complete; reviewed for 1.0.1 with final public/package evidence below |
| [widgets/README.md](../widgets/README.md) | Review unchanged workflow, labels and links; retain applicable guidance | Complete; reviewed for 1.0.1 with final public/package evidence below |

## Publication checks

- Add the tray guide to canonical reading order, wiki sidebar and offline navigation.
- Use actual owned Windows/Linux screenshots; do not label generated or mock images as runtime evidence.
- Validate all local links, images, literal UI labels and offline navigation with networking disabled.
- Compare the public wiki with canonical repository content and verify its pushed revision and rendered pages.
- Verify public Latest and freshly downloaded assets before calling the release available.
- Record changed/retained pages, exact tested commits and remaining limits here and in the release plan.

## Native launcher documentation checkpoint — 2026-10-05

README, tray controls, installation/update, Wine and UMU chapters now explain the native Linux candidate while retaining older manual workflows. The packaged setup screenshot is from protected run 37327204777 at tested merge 5ff394f9. The updated 21-page offline guide passes networking-disabled browser checks for local links/navigation, loaded images and reflow at 320/390/1366 pixels. These are preparation checks: the full page audit, public wiki synchronization, final packaged guide and public v1.0.1 availability remain outstanding.

## KDE illustration and recovery guidance — 2026-10-05

The tray guide adds an actual KDE Plasma Running-tooltip screenshot from native
source `2989331` with the protected `5ff394f9` Windows backend and owned sample
settings. It also explains using the editor's Backup and recovery controls if a
Linux panel closes without showing the fallback window. The generated guide now
contains **21 chapters and 18 images**. A fresh network-disabled browser run
passes local navigation, all screenshots and every chapter at 320/390/1366
pixels. The full page audit, wiki publication and final-package qualification
remain pending; this preparation evidence does not complete their rows.

## 1.0.1 publication preparation audit — 2026-10-05

All **80 Markdown documents** are scanned. The 21 current guide chapters are
reviewed against retained workflows and native launcher labels; advanced and
historical contracts remain available. The audit checks **495 local file and
heading links**, with zero unresolved targets. This link check supplements the
content review; it does not establish external-service behavior.

Updated content covers the README download/start/tray/quit flow, Wine/UMU setup,
Windows sign-in and hidden icons, update/restore/recovery, current feature
versions and accepted memory/KDE limitations. Automation and troubleshooting
now explain ordinary money amounts rather than requiring cents conversion;
literal rule buttons match the frontend. Contributor instructions correctly
explain fail-closed scanning of untrusted fork code. Three historical goal pages
explicitly identify their original status context; original evidence is retained.
The Rumble setup heading link is repaired. License/fixture provenance and
unchanged chat/finance/automation/custom permissions/HTTP contracts are retained.

New pages are [known issues](known-issues.md) and [1.0.1 notes](releases/1.0.1.md).
Release workflow and publisher tests reject mismatched sources, failed checks,
changed assets and conflicting immutable tags; **8 tests pass** on pinned
Python 3.14.7. The regenerated guide passes a real browser with networking denied:
**21 chapters / 18 screenshots**, every chapter at **320, 390 and 1366 pixels**,
working local navigation, loaded images and no horizontal page overflow.
Ignored local output: `release/guide-1.0.1-final-prepublication/`.

The dedicated wiki checkout is cloned from the verified project wiki remote.
All 21 chapters and 18 screenshots, plus the canonical sidebar, are prepared from
repository sources with rewritten links. Its push, exact remote SHA and live
render/navigation are still pending the release. Protected main artifacts,
publication and anonymous download/checksum evidence must be recorded before
calling G24 complete. Release instructions are prepared on the PR branch; this
preparation is not a claim that v1.0.1 is already public.


## Final documentation delivery — 2026-10-05

The reviewed README and every current guide page ship in published 1.0.1.
All 80 Markdown documents and 495 local file/heading links passed the preparation
review; unchanged historical/license/fixture contracts retain their provenance.
The exact-main packaged guide passed Windows network-disabled qualification in
[37384546952](https://github.com/TechDaddyKB/tdsblive/actions/runs/37384546952).
It contains 21 chapters and 18 screenshots. The all-chapter 320/390/1366-pixel
checks and literal UI label reviews remain recorded above.

After [1.0.1 publication](https://github.com/TechDaddyKB/tdsblive/releases/tag/v1.0.1),
all canonical wiki chapters, illustrations and navigation were pushed as
`72e23c8aaa7c6f1b85ed7fb935c67a65a2597767`; the remote master SHA matched.
Live Chrome verified Home's 1.0.1 download/version wording, Home → tray navigation,
the tray's five loaded native illustrations and sidebar → Linux Wine setup.
The README's release handoff and public Latest release were verified. Anonymous
HTTPS downloads of all four assets matched the source qualification checksums in
successful publisher run [37388415088](https://github.com/TechDaddyKB/tdsblive/actions/runs/37388415088).

G24's documentation/publication audit is complete within the accepted scope.
[The final release record](tray-release-plan.md#g24-final-delivery--2026-10-05)
retains exact source, hashes, CI and actual OBS recovery/audio evidence.
[Known issues](known-issues.md) preserve accepted limits and future investigation;
this audit does not claim unavailable human, physical or paid-platform evidence.
