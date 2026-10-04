# Observed UI usability sessions

Status: human script prepared; participants are unavailable (2026-10-04).
The operator subsequently authorized synthetic qualification in place of these
sessions. This human script is retained for optional follow-up and is no longer
a delivery blocker. It does not constitute observed human evidence.

## Synthetic delivery qualification

`tools/browser-qualification/ui-acceptance.mjs` runs three fresh-project scenarios:
1366×768/light/pointer, 768×1024/dark/pointer and 390×844/light/emulated touch.
Each starts from Overview, creates an automatically named overlay, adds a named
Twitch follow design and Ko-fi donation design with different wording, follows
the guided steps, tests both and verifies the copied OBS address. It uses only
visible user controls, does not automatically reveal hidden controls through the
legacy editor test helper, never types internal event identifiers and records
elapsed time and task milestones. Ten minutes is a regression ceiling, not a
claim about human learning time. It asserts event/ledger/automation isolation.

The same gate measures native 200%/400% Chromium zoom across all ten pages in both
themes, accessible control names/main landmarks, keyboard navigation, modal focus
containment/restoration and reduced-motion selection. The full legacy browser
suite covers the feature parity inventory. Protected Windows repeats the entire
suite against the shipped portable and installed executables. Machine-readable
results and screenshots are ignored test artifacts uploaded by CI.

No automated scenario claims three human participants, assistive-technology
speech, physical hardware gestures or human hearing. The optional human procedure
below remains useful after delivery.

## Setup

Use a disposable project with bot execution disabled, owned sample media and no
production credentials. Do not stream or record a broadcast. Record the exact
commit/build, operating system, browser, viewport, theme and input method. Give
each participant a fresh project and a maximum of ten minutes for the tasks
below. Use participant numbers rather than names or personal information.

The observer may read the task aloud and ask the participant to describe what
they expect. Do not identify controls, suggest internal identifiers, explain
code, or demonstrate a solution. Record any help requested; developer assistance
means the session does not meet the acceptance criterion. A participant may stop
at any time. Obtain their consent before recording their voice or screen.

## Read-aloud task

“You are setting up an overlay for your stream. Make one alert for a new Twitch
follower and another alert for a Ko-fi donation. Make their designs visibly
different, such as different wording or colors. Test both designs using sample
events. Finally, find the address you would give OBS to show this overlay. You
do not need to open OBS or connect an account.”

If completed early, ask the participant to create a second donation design for
at least $10, put the donation designs into a set that selects one matching
design, and test $5 and $10. This supplemental task diagnoses tier usability;
it does not replace the mandatory tasks.

## Recording sheet (copy for each participant)

| Field | Observation |
|---|---|
| Participant number | |
| Date; observer | |
| Exact commit; build/package | |
| OS; browser; theme | |
| Viewport; browser zoom; input method | |
| Prior overlay/technical experience | |
| Start / finish / elapsed time | |
| Created follow design; elapsed time | |
| Created donation design; elapsed time | |
| Designs visibly different | |
| Tested both; elapsed time | |
| Found OBS handoff; elapsed time | |
| Typed internal identifiers? Which task? | |
| Requested/received help? | |
| Hesitation, misclicks, unreadable content | |
| Resizing, focus, touch or accessibility difficulties | |
| Supplemental tier results | |
| Participant's words about hardest step | |
| Meets all mandatory criteria within ten minutes? | |
| Follow-up issue / fix / retest commit | |

## Acceptance and follow-up

All three participants must finish the mandatory tasks within ten minutes,
without typing internal identifiers or receiving developer assistance. Record
failures honestly, revise the UI, and repeat the affected flow before accepting those optional human observations. Keep consented recordings and detailed notes in private evidence
storage; publish only anonymized findings and artifact references. Automated browser tests do not constitute observed human sessions; they satisfy
the separately authorized synthetic delivery gate above.
