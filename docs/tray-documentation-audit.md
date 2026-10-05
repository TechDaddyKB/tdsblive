# Tray release documentation audit

This is the preparation checklist for G24, not its completion evidence. G22 and G23
must be accepted before final delivery. Every row requires a final review against
1.0.1 behavior and the published package; keyword discovery alone is not an audit pass.

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
| [docs/tray-documentation-audit.md](tray-documentation-audit.md) | Include new pages/assets and final package/wiki evidence | Pending |
| [docs/user-guide/Tray-and-Desktop-Controls.md](user-guide/Tray-and-Desktop-Controls.md) | Prepared illustrated Windows controls; Linux instructions and release status still require completion | Pending |
| [AGENTS.md](../AGENTS.md) | Retain workspace rules; verify release execution follows them | Pending |
| [CONTRIBUTING.md](../CONTRIBUTING.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [README.md](../README.md) | Download table, tray-first start/quit, Linux companion, published version | Pending |
| [SPEC.md](../SPEC.md) | Add desktop and release requirements without rewriting original numbered requirements | Pending |
| [docs/architecture.md](architecture.md) | Native companion, control ownership and external Linux runner boundaries | Pending |
| [docs/automation.md](automation.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [docs/database.md](database.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [docs/desktop-lifecycle.md](desktop-lifecycle.md) | Keep candidate boundaries current; Linux lifecycle after implementation | Pending |
| [docs/development.md](development.md) | Current goals and desktop build/qualification links | Pending |
| [docs/events.md](events.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [docs/foundation-api.md](foundation-api.md) | Desktop diagnostics and retained HTTP authority | Pending |
| [docs/g00-validation.md](g00-validation.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/g01-validation.md](g01-validation.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/g02-validation.md](g02-validation.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/g03-integrations.md](g03-integrations.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/g04-rumble-ingestion.md](g04-rumble-ingestion.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/g05-overlays-chat.md](g05-overlays-chat.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/g06-editor-alerts.md](g06-editor-alerts.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/g07-financial-ledger.md](g07-financial-ledger.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/g07-qualification.md](g07-qualification.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/g08-donor-widgets.md](g08-donor-widgets.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/g08-qualification.md](g08-qualification.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/g09-automation.md](g09-automation.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/g09-qualification.md](g09-qualification.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/g10-qualification.md](g10-qualification.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/g11-advanced-editor.md](g11-advanced-editor.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/g12-custom-widgets.md](g12-custom-widgets.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/g13-compatibility.md](g13-compatibility.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/implementation-plan.md](implementation-plan.md) | Goal status and acceptance evidence | Pending |
| [docs/installation.md](installation.md) | Current release evidence and tray/companion installation links | Pending |
| [docs/licenses/SimpleIcons-CC0.md](licenses/SimpleIcons-CC0.md) | Preserve fixture/license provenance; verify no tray-related instructions need changes | Pending |
| [docs/obs-mcp.md](obs-mcp.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [docs/overlays.md](overlays.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [docs/platform-logos.md](platform-logos.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [docs/releases/1.0.0.md](releases/1.0.0.md) | Retain as unpublished build history; link cumulative 1.0.1 notes | Pending |
| [docs/requirements-matrix.md](requirements-matrix.md) | Add TR requirements and exact current acceptance evidence | Pending |
| [docs/rumble-analysis.md](rumble-analysis.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/rumble.md](rumble.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [docs/security.md](security.md) | Authenticated desktop pipes and Linux launcher settings | Pending |
| [docs/testing.md](testing.md) | Native desktop, runner, package and publication qualification | Pending |
| [docs/tray-release-plan.md](tray-release-plan.md) | Exact current evidence and remaining gates | Pending |
| [docs/ui-manual-qualification.md](ui-manual-qualification.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/ui-parity-checklist.md](ui-parity-checklist.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/ui-redesign-completion-audit.md](ui-redesign-completion-audit.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/ui-redesign-plan.md](ui-redesign-plan.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/ui-redesign-qualification.md](ui-redesign-qualification.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/ui-usability-test.md](ui-usability-test.md) | Preserve historical requirements/evidence; verify current navigation and version context | Pending |
| [docs/user-guide/Adaptive-Editor-and-Guided-Alerts.md](user-guide/Adaptive-Editor-and-Guided-Alerts.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [docs/user-guide/Advanced-Editor-and-Widgets.md](user-guide/Advanced-Editor-and-Widgets.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [docs/user-guide/Automation.md](user-guide/Automation.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [docs/user-guide/Backup-and-Recovery.md](user-guide/Backup-and-Recovery.md) | Prepared save-before-tray-action and Cancel-first recovery guidance; final Linux/package review remains | Pending |
| [docs/user-guide/Before-You-Begin.md](user-guide/Before-You-Begin.md) | Prepared explicit public v0.1.0 versus unpublished v1.0.1 guidance; update after publication | Pending |
| [docs/user-guide/Chat-and-OBS.md](user-guide/Chat-and-OBS.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [docs/user-guide/Custom-Widgets-and-Portable-Packages.md](user-guide/Custom-Widgets-and-Portable-Packages.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [docs/user-guide/Everyday-Use.md](user-guide/Everyday-Use.md) | Prepared manual/quiet launch, save and end-of-stream tray quit; final Linux/package review remains | Pending |
| [docs/user-guide/First-Setup.md](user-guide/First-Setup.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [docs/user-guide/Glossary.md](user-guide/Glossary.md) | Prepared plain-language tray, companion and sign-in definitions | Pending |
| [docs/user-guide/Home.md](user-guide/Home.md) | Tray guide navigation, downloads and exact qualification limits | Pending |
| [docs/user-guide/Install-and-Update.md](user-guide/Install-and-Update.md) | Prepared tray quit before file replacement and fallback close behavior; four assets/native launcher remain | Pending |
| [docs/user-guide/Install-on-Linux-Proton.md](user-guide/Install-on-Linux-Proton.md) | Native UMU launcher, explicit runner/prefix and native browser | Pending |
| [docs/user-guide/Install-on-Linux-Wine.md](user-guide/Install-on-Linux-Wine.md) | Native first-run launcher, existing prefix, direct Wine and Bottles limit | Pending |
| [docs/user-guide/Install-on-Windows.md](user-guide/Install-on-Windows.md) | Prepared illustrated automatic editor, hidden tray icons, quiet startup and quit; final native/package review remains | Pending |
| [docs/user-guide/LAN-Access.md](user-guide/LAN-Access.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [docs/user-guide/Local-StreamElements-Compatibility.md](user-guide/Local-StreamElements-Compatibility.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [docs/user-guide/Overlays-and-Alerts.md](user-guide/Overlays-and-Alerts.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [docs/user-guide/Supporter-Totals.md](user-guide/Supporter-Totals.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [docs/user-guide/Troubleshooting.md](user-guide/Troubleshooting.md) | Prepared missing Windows tray, stopped companion/backend and browser recovery guidance; native Linux remains | Pending |
| [docs/widgets.md](widgets.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [integrations/streamerbot/kofi-forwarding.md](../integrations/streamerbot/kofi-forwarding.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [streamerbot/bootstrap/README.md](../streamerbot/bootstrap/README.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [streamerbot/examples/README.md](../streamerbot/examples/README.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [streamerbot/import/README.md](../streamerbot/import/README.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |
| [tests/fixtures/donors/README.md](../tests/fixtures/donors/README.md) | Preserve fixture/license provenance; verify no tray-related instructions need changes | Pending |
| [tests/fixtures/media/README.md](../tests/fixtures/media/README.md) | Preserve fixture/license provenance; verify no tray-related instructions need changes | Pending |
| [tests/fixtures/rumble/README.md](../tests/fixtures/rumble/README.md) | Preserve fixture/license provenance; verify no tray-related instructions need changes | Pending |
| [widgets/README.md](../widgets/README.md) | Review unchanged workflow, labels and links; retain applicable guidance | Pending |

## Publication checks

- Add the tray guide to canonical reading order, wiki sidebar and offline navigation.
- Use actual owned Windows/Linux screenshots; do not label generated or mock images as runtime evidence.
- Validate all local links, images, literal UI labels and offline navigation with networking disabled.
- Compare the public wiki with canonical repository content and verify its pushed revision and rendered pages.
- Verify public Latest and freshly downloaded assets before calling the release available.
- Record changed/retained pages, exact tested commits and remaining limits here and in the release plan.
