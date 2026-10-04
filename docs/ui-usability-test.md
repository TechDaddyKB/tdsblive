# Observed UI usability sessions

Status: prepared; participants are not available yet (operator response, 2026-10-04).
This document does not constitute usability evidence. G20 remains open until
three nontechnical participants complete observed sessions on the candidate build.

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
failures honestly, revise the UI, and repeat the affected flow before G20 is
complete. Keep consented recordings and detailed notes in private evidence
storage; publish only anonymized findings and artifact references. Automated
browser tests, screenshots and developer walkthroughs do not satisfy this gate.
