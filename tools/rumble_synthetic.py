#!/usr/bin/env python3
"""Generate deterministic G00 edge-case inputs, not production event assertions.

Expectations are acceptance oracles for the later G04 adapter. Validating these
files now does not prove a Rumble event engine exists or meets the expectations.
"""
from copy import deepcopy
from datetime import datetime, timedelta, timezone

from rumble_evidence import FIXTURES, scan_bytes, encode, write_json

TIME = datetime(2026, 1, 1, tzinfo=timezone.utc)


def stamp(seconds):
    return (TIME + timedelta(seconds=seconds)).isoformat().replace("+00:00", "Z")


def message(number=1, user="synthetic-user-a", text=None, created=None):
    return {"username": user, "profile_pic_url": "https://example.invalid/avatar-a",
            "badges": ["premium"], "text": text or f"Synthetic message {number}",
            "created_on": created or stamp(number)}


def rant(number=1, cents=100):
    return {**message(number), "amount_cents": cents, "amount_dollars": cents // 100,
            "expires_on": stamp(number + 120)}


def snapshot(messages=(), rants=(), followers=(), stream="synthetic-stream-a", live=True):
    result = {
        "now": int(TIME.timestamp()), "type": "user", "user_id": "synthetic-account-a",
        "username": "synthetic-channel-owner", "channel_id": None, "channel_name": None,
        "since": None, "max_num_results": 50,
        "followers": {"num_followers": len(followers), "num_followers_total": len(followers),
                      "latest_follower": deepcopy(followers[0]) if followers else None,
                      "recent_followers": deepcopy(list(followers))},
        "subscribers": {"num_subscribers": 0, "latest_subscriber": None, "recent_subscribers": []},
        "gifted_subs": {"num_gifted_subs": 0, "latest_gifted_sub": None, "recent_gifted_subs": []},
        "livestreams": [],
    }
    if stream is not None:
        result["livestreams"] = [{
            "id": stream, "is_live": live, "title": "Synthetic stream", "created_on": stamp(0),
            "scheduled_on": stamp(0), "categories": {"primary": {"slug": "gaming", "title": "Gaming"}},
            "watching_now": 10, "likes": 1, "dislikes": 0, "visibility": "public",
            "stream_key": "[REDACTED]", "server_url": "https://example.invalid/ingest",
            "chat": {"latest_message": deepcopy(messages[0]) if messages else None,
                     "recent_messages": deepcopy(list(messages)),
                     "latest_rant": deepcopy(rants[0]) if rants else None,
                     "recent_rants": deepcopy(list(rants))},
        }]
    return result


def poll(payload=None, outcome="ok", status=200, **metadata):
    return {"kind": "poll", "outcome": outcome, "http_status": status,
            "payload": deepcopy(payload), **metadata}


def scenario(identifier, description, steps, expected, origin="observed-shape-derived", **extra):
    steps = [deepcopy(step) for step in steps]
    for index, step in enumerate(steps):
        step["sequence"] = index + 1
        step["observed_at"] = stamp(1000 + index * 7)
    return {"formatVersion": 1, "id": identifier, "provenance": "synthetic",
            "schemaOrigin": origin, "description": description, "steps": steps,
            "expected": expected,
            "expectationScope": "G04 acceptance oracle; not a live-capture or current-adapter verification",
            **extra}


def cases():
    a, b = message(1), message(2)
    follower = {"username": "synthetic-user-a", "profile_pic_url": "https://example.invalid/avatar-a",
                "followed_on": stamp(1)}
    r = rant()
    fresh_rant = rant(1006)
    full = [message(n) for n in range(50, 0, -1)]
    baseline = snapshot([a], [r], [follower])
    data = [
        scenario("baseline-repetition", "Historical messages, Rants and follows never alert on baseline or repeats.",
                 [poll(baseline)] * 5, {"chat": 0, "rants": 0, "follows": 0, "online": 0}),
        scenario("duplicate-occurrence-increase", "Identical same-user/same-time messages increase the snapshot multiset.",
                 [poll(snapshot([a])), poll(snapshot([a, a])), poll(snapshot([a, a]))], {"chat": 1}),
        scenario("indistinguishable-window-replacement", "Same count and identical records cannot prove whether a new message replaced an old one.",
                 [poll(snapshot([a, a])), poll(snapshot([a, a]))], {"chat": 0, "deliveryGuarantee": False},
                 limitation="No stable source identity can distinguish these histories."),
        scenario("same-text-different-users", "Identical text and timestamp from another user is a distinct record.",
                 [poll(snapshot([a])), poll(snapshot([message(1, user="synthetic-user-b"), a]))], {"chat": 1}),
        scenario("same-text-new-time", "Same user repeats text at a different creation timestamp.",
                 [poll(snapshot([a])), poll(snapshot([message(2, text=a["text"]), a]))], {"chat": 1}),
        scenario("array-reordering", "Reordered unchanged entries do not create events.",
                 [poll(snapshot([b, a])), poll(snapshot([a, b])), poll(snapshot([b, a]))], {"chat": 0}),
        scenario("full-window-rotation", "One new entry rotates a full window with 49 overlapping records.",
                 [poll(snapshot(full)), poll(snapshot([message(51)] + full[:-1]))], {"chat": 1, "possibleGaps": 0}),
        scenario("full-window-zero-overlap", "Two full windows with no overlap may conceal lost messages.",
                 [poll(snapshot(full)), poll(snapshot([message(n) for n in range(100, 50, -1)]))],
                 {"chat": 50, "possibleGaps": 1, "inventedMessages": 0}),
        scenario("empty-chat-window", "Empty recent arrays do not imply an offline stream.",
                 [poll(snapshot([a])), poll(snapshot()), poll(snapshot([a]))], {"chat": 0, "offline": 0}),
        scenario("errors-and-offline-debounce", "HTTP failures do not advance the two-success offline counter.",
                 [poll(snapshot([a])), poll(outcome="network_error", status=None),
                  poll(outcome="http_error", status=429, retry_after="60"),
                  poll(outcome="http_error", status=500), poll(snapshot(stream=None)),
                  poll(outcome="network_error", status=None), poll(snapshot(stream=None))],
                 {"offline": 1, "offlineAtStep": 7, "chat": 0}),
        scenario("retry-after-date", "HTTP-date Retry-After must be honored independently of the local backoff cap.",
                 [poll(snapshot()), poll(outcome="http_error", status=429,
                                        retry_after="Thu, 01 Jan 2026 00:18:00 GMT"), poll(snapshot([a]))],
                 {"chat": 1, "offline": 0, "honorRetryAfter": True}),
        scenario("online-offline-recovery", "Live recovery clears pending offline confirmation.",
                 [poll(snapshot(stream=None)), poll(snapshot()), poll(snapshot(stream=None)),
                  poll(snapshot()), poll(snapshot(stream=None)), poll(snapshot(stream=None))],
                 {"online": 1, "offline": 1, "offlineAtStep": 6}),
        scenario("explicit-not-live", "False is_live is also a successful offline observation.",
                 [poll(snapshot()), poll(snapshot(live=False)), poll(snapshot(live=False))], {"offline": 1}),
        scenario("application-restart", "Restart persists seen identities and baselines new downtime history without alerts.",
                 [poll(snapshot([a])), poll(snapshot([b, a])), {"kind": "control", "op": "restart"},
                  poll(snapshot([message(3), b, a])), poll(snapshot([message(4), message(3), b, a]))],
                 {"chat": 2, "noHistoricalAlertsAtStep": 4}),
        scenario("credential-change", "Credential context changes establish a new baseline.",
                 [poll(snapshot([a])), {"kind": "control", "op": "credential-change", "context": "synthetic-account-b"},
                  poll(snapshot([b, a])), poll(snapshot([message(3), b, a]))], {"chat": 1}),
        scenario("dedupe-reset", "Explicit reset establishes a baseline rather than replaying the window.",
                 [poll(snapshot([a])), {"kind": "control", "op": "dedupe-reset"},
                  poll(snapshot([b, a])), poll(snapshot([message(3), b, a]))], {"chat": 1}),
        scenario("stream-id-change", "New stream identity has independent baseline; prior stream disappearance is debounced.",
                 [poll(snapshot([a])), poll(snapshot([a], stream="synthetic-stream-b")),
                  poll(snapshot([b, a], stream="synthetic-stream-b"))],
                 {"chat": 1, "online": 1, "offline": 1, "oldStreamOfflineAtStep": 3}),
        scenario("rant-repeat-expiry", "A newly observed Rant persists once even when repeated or later absent.",
                 [poll(snapshot()), poll(snapshot(rants=[fresh_rant])), poll(snapshot(rants=[fresh_rant])), poll(snapshot()),
                  poll(snapshot(rants=[fresh_rant]))], {"rants": 1, "financialEntries": 1, "usdAmountMinor": 100}),
        scenario("rant-cents-authority", "Cents override a conflicting dollars display; never use binary float money.",
                 [poll(snapshot()), poll(snapshot(rants=[{**rant(1006, cents=125), "amount_dollars": 99}]))],
                 {"rants": 1, "usdAmountMinor": 125, "amountConflictDiagnostic": True}),
        scenario("new-follow", "A new timestamped follower entry fires once.",
                 [poll(snapshot(followers=[follower])),
                  poll(snapshot(followers=[{**follower, "username": "synthetic-user-b", "followed_on": stamp(2)}, follower]))],
                 {"follows": 1}),
        scenario("subscriber-official-example", "Documentation-derived subscriber example, not captured live evidence.",
                 [poll(snapshot()), poll(snapshot())], {"subscriptions": 1, "liveVerified": False},
                 origin="official-example-derived"),
        scenario("gift-mutation-unverified", "A remaining-gifts decrement is not a new purchase; authoritative gifts stay gated.",
                 [poll(snapshot()), poll(snapshot()), poll(snapshot())],
                 {"authoritativeGifts": 0, "financialEntries": 0, "liveVerified": False},
                 origin="official-example-derived"),
        scenario("unknown-null-type-drift", "Future shapes must be retained diagnostically without killing the poller.",
                 [poll(snapshot()), poll(snapshot())], {"offline": 0, "preserveUnknown": True},
                 origin="hypothetical-schema-change"),
        scenario("malformed-payload-recovery", "Malformed JSON and bad shape are health failures, not offline.",
                 [poll(snapshot()), poll(outcome="invalid_json", wire_body="{not-json"),
                  poll(outcome="unexpected_json", payload={"error": "synthetic-error"}), poll(snapshot([a]))],
                 {"chat": 1, "offline": 0}, origin="hypothetical-error"),
        scenario("stats-and-count-only-follow", "Viewer/like zero changes are valid; follower counters alone do not prove new follows.",
                 [poll(snapshot()), poll(snapshot())], {"viewerChanges": 1, "likeChanges": 1, "follows": 0}),
        scenario("badge-mutation-ambiguity", "Changing a mutable badge can change a naive full fingerprint of the same message.",
                 [poll(snapshot([a])), poll(snapshot([{**a, "badges": ["admin"]}]))],
                 {"requireExplicitMutationPolicy": True}, origin="hypothetical-schema-change"),
    ]
    sub = {"username": "synthetic-subscriber", "user": "synthetic-subscriber",
           "amount_cents": 500, "amount_dollars": 5, "subscribed_on": stamp(2)}
    subscription = next(c for c in data if c["id"] == "subscriber-official-example")
    subscription["steps"][1]["payload"]["subscribers"] = {
        "num_subscribers": 1, "latest_subscriber": sub, "recent_subscribers": [sub]}
    gifts = next(c for c in data if c["id"] == "gift-mutation-unverified")
    gift = {"total_gifts": 5, "gift_type": "rumble", "remaining_gifts": 5,
            "video_id": 101010, "purchased_by": "synthetic-gifter"}
    for index in (1, 2):
        item = {**gift, "remaining_gifts": 6 - index}
        gifts["steps"][index]["payload"]["gifted_subs"] = {
            "num_gifted_subs": 5, "latest_gifted_sub": item, "recent_gifted_subs": [item]}
    drift = next(c for c in data if c["id"] == "unknown-null-type-drift")
    drift["steps"][1]["payload"].update(channel_id="synthetic-channel-b", since=1,
                                         future_data={"nested": [None, {"synthetic": True}]})
    drift["steps"][1]["payload"]["subscribers"]["latest_subscriber"] = {"future_field": "synthetic-value"}
    stats = next(c for c in data if c["id"] == "stats-and-count-only-follow")
    stats["steps"][1]["payload"]["livestreams"][0].update(watching_now=0, likes=0)
    stats["steps"][1]["payload"]["followers"].update(num_followers=10, num_followers_total=10)
    expiry = next(c for c in data if c["id"] == "rant-repeat-expiry")
    expiry["steps"][3]["observed_at"] = stamp(1140)
    expiry["steps"][4]["observed_at"] = stamp(1147)
    return data


def main():
    output = FIXTURES / "synthetic"
    scenarios = cases()
    # Scan the complete uncompressed content before writing any public fixture.
    scan_bytes(b"".join(encode(c) for c in scenarios), "synthetic fixtures")
    for case in scenarios:
        write_json(output / (case["id"] + ".json"), case)
    write_json(output / "index.json", {
        "formatVersion": 1, "provenance": "synthetic", "cases": [c["id"] for c in scenarios],
        "warning": "Inputs and proposed expectations only. G04 must execute these against the real adapter.",
    })
    print(f"Generated {len(scenarios)} synthetic scenarios.")


if __name__ == "__main__":
    main()
