# Ko-fi financial forwarding

Streamer.bot remains the Ko-fi connection and trigger authority. The donation trigger's [documented arguments](https://docs.streamer.bot/api/triggers/integrations/ko-fi/donation) include `amount`, `currency`, `from`, `messageId` and `timestamp`. TDSBLive does not assume an unpublished native Ko-fi WebSocket schema.

1. Import the G03 TDSBLive actions, or create an action with the Execute C# Code sub-action containing [TDSBLiveForwardEvent.cs](TDSBLiveForwardEvent.cs).
2. Add the Ko-fi Donation trigger to that action. Before the C# sub-action, set `tdsbliveForwardedSource` to `Kofi` and `tdsbliveForwardedType` to `Donation` using Set Argument sub-actions. Preserve the trigger's native `messageId`, `amount`, `currency`, `from` and `timestamp`; do not replace the ID with a new random ID on retries.
3. For a test, set `isTest` to `true` and supply owned arguments. This forces simulation provenance and excludes the event from financial history. Production trigger execution must not retain the synthetic test argument; remove that test-only Set Argument sub-action before enabling the real trigger.
4. TDSBLive subscribes to `General.Custom` and unwraps this explicitly routed envelope. Native currency amounts require a supported ISO minor-unit scale and exact representable precision. Invalid amounts remain gated; unsupported/missing currencies cannot become USD. No nominal donation price is substituted.

The source timestamp determines the historical UTC rate date. Prefer an ISO timestamp with an explicit offset when constructing a test. Ko-fi `from` is a display name, not a verified platform account ID: repeated anonymous/unidentified donors are not automatically merged by that name. Link identities manually only when ownership is established. Remove private message forwarding from the generic helper's allowlist if it is not wanted; financial accounting needs no message text.

The helper uses an explicit allowlist and never forwards arbitrary action arguments or credentials. Sending the same source event again deduplicates by Ko-fi/native ID. Direct Ko-fi WebSocket events remain gated as `kofi_websocket_schema_unverified`; attach this forwarding action to the Ko-fi trigger to supply the documented contract.

Run `python tools/qualify_kofi.py --execute-synthetic-forwarding-action` only against the installed local bot with the imported G03 forwarder. It starts an isolated temporary host, executes that single action with owned test arguments, verifies normalized USD cents and simulation exclusion, then removes its own temporary data. It does not make a payment or claim live paid-event observation.
