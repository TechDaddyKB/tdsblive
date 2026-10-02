# Donor widgets (G08)

Implementation is in progress. This document describes the current worktree contracts; it is not completion evidence.

The canvas supports `donor-crown`, `donor-leaderboard`, `latest-supporter`, `current-stream-leader`, and `current-stream-total`. Donor configuration is persisted in each widget's `donor` object and participates in ordinary overlay revisions. Existing documents without donor settings receive defaults. No financial table migration is currently required.

Periods use the saved financial timezone. Custom date ranges are half-open local dates; current-stream widgets require a saved UTC stream start, and never infer a boundary from host startup. Platforms and support kinds filter source contributions before identity aggregation. Empty filter arrays include all sources. Minimum amounts use an integer-string USD-cent threshold; ranked lists accept 1–25 supporters. Linked platform identities aggregate under their current supporter identity. Replay and simulation never enter production totals.

## Overlay protocol

The existing `/ws/overlay/{id}` connection delivers an additional server envelope:

```json
{
  "op": "donors",
  "widgets": [
    {
      "widgetId": "saved-widget-guid",
      "state": "ready",
      "generatedAt": "2026-10-01T12:00:00Z",
      "rows": [],
      "totalUsdMinor": "1250",
      "unknownCount": 0,
      "gatedCount": 0,
      "estimatedCount": 0
    }
  ]
}
```

Rows expose only supporter display identity, integer-string amount, contributing platforms, uncertainty counts, latest timestamp, `hasKnownAmount`, and an optional sanitized avatar URL. Source payloads, platform identity keys, contribution IDs and credentials are excluded. Amount strings are authoritative; clients must not convert them through JavaScript `Number`.

States are `ready`, `empty`, `pending`, `gated`, `period-unavailable`, and `preview`. Unknown and gated contributions are excluded from amounts and disclosed separately. Nominal and estimated FX values remain labeled. An unvalued latest supporter may display their name with “Awaiting valuation”; unresolved amounts cannot win the crown.

Snapshots are currently checked once per second per connected overlay and pushed only when aggregate content changes. Opening/reopening the socket receives a current snapshot. Saved overlay changes update configured widgets on that same connection. Database failures close the connection so the existing bounded reconnect strategy can obtain a fresh snapshot. Preview connections receive an isolated empty preview state, never production totals. Scoped overlay-token authorization and revocation continue to apply; donor delivery does not grant access to `/api/financial` administration.

Queries filter indexed time/platform fields and group contributions in SQL by supporter and platform. A connection-local exact integer aggregate returns decimal strings using `BigInteger`, avoiding SQLite integer-sum overflow and floating-point fallback. Only grouped totals and at most 25 displayed supporters' avatar metadata reach application memory. The aggregate registration follows [Microsoft.Data.Sqlite's documented aggregate-function API](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/user-defined-functions#aggregate-functions). Precision and linked-identity tests exercise this actual SQLite path.

Crown images and font assets use authenticated asset requests with header credentials, never credential query parameters. Asset MIME and reference validation occurs when overlays save or restore. Downloaded object URLs and font faces are released when configuration changes or the renderer unmounts. Missing assets retain built-in appearance. Avatar metadata accepts only HTTP(S) URLs without embedded credentials, query strings or fragments; absent avatars are shown as unavailable. HTTP local and authenticated LAN operation remain supported; HTTPS is not required.

## Remaining qualification

Local qualification covers actual SQLite ingestion/projection, period boundaries, precision/performance, and populated-browser filters, identity changes, reconciliation, editor persistence/revision restore, reconnect, preview isolation, decoded images, loaded fonts and animated transitions. Windows run 36960170546 verifies the source with passing backend/frontend/browser checks, OpenCover/LCOV import and SonarQube quality gate. The operator confirms actual OBS rendering and live refresh. Platform labels now render as bundled vector logos with accessible hover names and inherited widget color/size; the operator also confirms the corrected logos in OBS. Windows run 36964748598 and its Sonar gate pass for the corrected source; protected PR #10 is merged as `9deb7c2b7aebbe401c22cfdf9ffaea4427f73d02`. The maintained [qualification audit](g08-qualification.md) records evidence and remaining delivery checks.
