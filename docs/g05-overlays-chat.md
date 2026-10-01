# Combined chat and overlay runtime (G05)

The host supports HTTP. HTTPS is not required for loopback or optional authenticated LAN operation. Streamer.bot remains the platform authority; this runtime displays normalized events and does not post chat or execute automation.

## Use in OBS or a browser

Start the host and open `http://127.0.0.1:17474/editor`. The Combined chat panel saves settings without restarting viewers. Add `http://127.0.0.1:17474/overlay/combined-chat` as an OBS Browser Source (for example, 1920×1080). Its page is transparent and contains no editor controls. Add `http://127.0.0.1:17474/chat/combined-chat` under OBS Docks → Custom Browser Docks, or open it in an ordinary browser. The streamer view keeps messages visible, allows scrolling without interrupting reading, and remembers its Light/Dark selection in that browser's local storage.

OBS must include the obs-browser plugin; a native installation without Browser Source cannot qualify this integration. See [OBS browser-source documentation](https://obsproject.com/browser-source) and the [official plugin](https://github.com/obsproject/obs-browser).

All four platform feeds share one WebSocket per page. Twitch and YouTube native events are consumed from Streamer.bot; Kick availability depends on the installed Streamer.bot version and may require the explicit forwarding action documented in G03. Rumble uses G04's persistent snapshot reconciliation. Review [Twitch ChatMessage](https://docs.streamer.bot/api/websocket/events/twitch/chat-message), [YouTube Message](https://docs.streamer.bot/api/websocket/events/youtube/message), and [Kick ChatMessage](https://docs.streamer.bot/api/websocket/events/kick/chat-message). Updated forwarding source preserves event/message identifiers, timestamps, avatars and badges; regenerate and import the bundle when updating an installed forwarder. Installing source in the repository does not automatically replace a running action.

## Settings and delivery

Settings include enabled platforms, platform icons, avatars, badges, usernames, timestamps, message text, platform colors, font family or licensed uploaded font, size, background opacity, duration, maximum messages, fade/slide/no animation, newest-first order, persistent display, and ignored usernames, prefixes and known bots. Badge image URLs supplied by Streamer.bot are preserved and rendered, with text fallback for missing, unsafe or failed images. Old saved messages retain their original metadata. User filters match IDs, logins or display names without case sensitivity. Empty platform selection hides all chat. Default overlay messages expire after 30 seconds; the streamer view remains persistent regardless of overlay expiry settings.

The server filters chat by the saved overlay definition before delivery and strips raw payloads and monetary metadata. Default viewers receive live provenance only. Explicit `?preview=1` enables synthetic/replay display; previews are labelled and do not invoke production automation or alter financial totals. History contains at most 500 live chat events before settings filters; preview events are delivered live to explicitly opted-in viewers, rather than restored from live history.

The browser retains at most 500 rendered messages and 10,000 recent event IDs, reconciles history with incoming events, and retries disconnected sockets with jittered backoff. This is bounded duplicate suppression, not an unlimited exactly-once delivery guarantee. The server uses bounded subscriber queues, closes slow consumers, and limits connections; reconnect restores available live history. Saved configuration is versioned optimistically: a stale save returns 409 and requires reload. Event history and G04 ingestion checkpoints survive restart.

## Public interfaces and access

The generated OpenAPI contract is `docs/contracts/openapi.json`; generated editor types are checked against it. Routes are:

| Route | Purpose |
|---|---|
| `GET /overlay/{id}`, `GET /chat/{id}` | Transparent runtime and streamer shell |
| `GET /api/overlays/{id}` | Saved definition |
| `PUT /api/overlays/{id}` | Validate and save definition with version precondition |
| `GET /api/overlays/{id}/chat` | Filtered live history |
| `/ws/overlay/{id}` | Chat-only subscription, settings updates and heartbeat |
| `GET, POST /api/overlays/{id}/tokens` | List metadata or mint a limited token |
| `DELETE /api/overlays/{id}/tokens/{tokenId}` | Revoke token |
| `GET, POST /api/assets` | List metadata or upload binary content |
| `GET /assets/{sha256}` | Serve validated content by ID |

Mutations require the established administrator authorization and CSRF protection. LAN viewers use a separate read-only token scoped to one overlay; the editor generates a private URL with the token in its fragment. Keep that URL private. REST requests send it as a bearer credential, and WebSocket authentication uses a subprotocol rather than a query string. Only its SHA-256 hash is persisted. Tokens expire after 1–365 days (default 30), can be revoked, and cannot access editor diagnostics, other overlays or mutations. Asset access additionally requires `X-TDSBLive-Overlay` and an asset referenced by that overlay's font setting. Public HTML/JS shells expose no chat history or credentials. Revocation closes limited sockets for the affected overlay; other valid viewers reconnect. Expiry is checked during socket use. Raw-event permission is not implemented for this built-in chat viewer.

## Assets and migration

Upload raw binary content with its MIME type and `X-Asset-Filename`; fonts additionally require `X-Asset-License`. Maximum size is 20 MiB, including unknown-length requests; SVG has a 1 MiB limit and bounded XML complexity. Accepted families are PNG, JPEG, WebP, GIF, SVG, MP3, WAV, Ogg, WebM, MP4, WOFF, WOFF2, TTF and OTF. Binary validation checks MIME signatures, not complete codec decoding. SVG uses a restricted geometric whitelist and removes script, event handlers, remote references and unsupported markup; elaborate SVG artwork can lose features. Fonts require a supplied license declaration, which is not independent legal verification.

Validated bytes are SHA-256 deduplicated. SQLite metadata records ID, filename, MIME, size, hash, upload time, sanitation and license information. Content lives under the configured data directory, with atomic writes and bounded upload concurrency; filenames never become filesystem paths. Assets, databases and generated runtime output stay out of Git.

Migration `20261001150413_OverlayRuntime` adds overlay, asset and hashed-token tables without changing existing events or Rumble checkpoints. Default `combined-chat` is created on initialization. Tests migrate down to G04 and back up and verify that existing chat survives; the new `IsBot` user field defaults to false for older records. Do not downgrade a production database without a backup.

## Qualification evidence and limits

On 2026-10-01, the isolated host connected to the user's installed Streamer.bot. The user posted the agreed Twitch test message and confirmed it appeared once with the theme button working. The history check found exactly one matching message; the overlay response excludes raw payload. The test host uses temporary data with outgoing automation disabled.

Linux tests currently pass 217 backend tests, with two Windows-only tests skipped, plus 49 frontend tests. Fresh isolated Chromium qualification verifies transparent rendering, escaped four-platform synthetic messages, bounded DOM, a single socket, reconnect without duplicate history, saved settings, streamer chat, and theme persistence. Foundation qualification verifies contract consistency and restart behavior. Existing replay qualification exercises all 785 sanitized Rumble polls through the actual reconciliation engine and SQLite store; replay alone does not prove a live Rumble message appearing in OBS.

The user confirmed actual OBS Browser Source transparency and Custom Browser Dock rendering/theme switching on 2026-10-01, and identified label-only badges. Badge artwork preservation/rendering was then implemented; the user then sent a fresh Twitch message and confirmed badge images display. Backend history independently contains the supplied badge artwork. Live YouTube/Kick chat has not been exercised with connected accounts; those shapes have documented-payload and synthetic tests. The initial Windows run 36889440276 passed build, tests and browser qualification but failed the SonarQube security gate (92.1% new-code coverage). Run 36892792188 imported 92.4% new-code coverage and cleared all but one token-ID finding. Strict GUID validation now rejects malformed token identifiers before any mutation, alongside encoded path identifiers, secure reconnect jitter and bounded generated regex validation. The corrected Windows CI, imported OpenCover/LCOV coverage and SonarQube results must be recorded in the implementation plan before G05 is marked complete.


## Emotes and GIFs

G05 includes Twitch, 7TV, BetterTTV and FrankerFaceZ artwork supplied by Streamer.bot. The adapter preserves structured message parts (`type`, `text`, `imageUrl`, `source`, `zeroWidth`) and positioned native emotes (`StartIndex`, inclusive `EndIndex`, `Name`, `ImageUrl`, `Type`; camel-case aliases also accepted). Valid UTF-16 spans preserve surrounding text and emoji; overlapping, mismatched and invalid spans fall back to text. Explicit GIF parts preserve their `url`/`imageUrl`, including GIF-only messages. Ordinary chat links never become automatic media embeds.

The optional canonical `message.parts` contract contains `{ kind, text, imageUrl?, source?, zeroWidth? }`; kinds are text, emote and gif. Existing plain-text messages remain compatible and older history is not retroactively enriched. Images use credential-free HTTP(S) URLs, no-referrer loading, escaped text fallback, inline emote sizing and bounded GIF dimensions (240×160 maximum). The browser naturally animates supplied GIF/WebP/APNG artwork. Zero-width flags position modifier artwork over the preceding inline emote. No embedded HTML, remote scripts, iframes or provider extensions are executed. Parts are limited to 256 and positioned emotes to 128 per message. Overlay display filters and message expiry still apply to media messages.

Streamer.bot remains the catalog and platform authority; provider availability follows its enabled/cached emote sources and the event metadata it supplies. [Its emote discovery API](https://docs.streamer.bot/api/websocket/requests) exposes native, BTTV, FFZ and 7TV catalogs. A read-only check on the actual installed instance returned all four catalogs, including 89 BTTV, 15 FFZ and 48 7TV entries. This is catalog evidence, not proof that a sent message rendered. Native/provider media normalization, durable history, unsafe URLs, fallbacks, Unicode spans and a fresh-browser owned two-frame GIF fixture are tested. The user verified native Twitch, BTTV, FFZ and 7TV emotes rendered; backend history independently records all four image sources. The first live GIF failed because blanket query-URL redaction removed the supplied URL. A shape-only observation confirmed Streamer.bot parts with `type: gif` and `url`, without a text field. The targeted public-CDN redaction correction is tested through persistence and overlay socket; The user confirmed the live GIF rendered, then identified a duplicate description beside it. GIF-only descriptions now remain alt/hover text; older saved G05 messages receive the same rendering correction. Backend, frontend and fresh-browser checks pass; final operator confirmation of the caption correction is pending.

[Twitch documents GIF keyboard eligibility and channel settings](https://help.twitch.tv/s/article/chat-basics). Support for a supplied Streamer.bot GIF payload does not establish eligibility for every account/channel or prove an unobserved live payload shape. Missing live GIF evidence must remain explicit.


GIF URLs must remain unchanged, including public attribution query parameters, per [Twitch IRC documentation](https://dev.twitch.tv/docs/chat/irc). Credential redaction permits this only inside explicitly typed GIF metadata, for recognized Giphy media hosts under `/media/` ending in `.gif`, with `cid`, `ep`, `rid` and `ct` query keys. Userinfo, fragments, credential assignments, known vault values, other endpoints/hosts and other query keys are rejected. Ordinary URLs retain the existing blanket query redaction. No GIF URL is reconstructed from an ID. Other future CDN/query shapes require evidence and explicit validation before support is claimed.
