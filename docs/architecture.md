# Architecture

TDSBLive is a hybrid Streamer.bot companion, not an inline C# application.
Streamer.bot remains the integration/action authority. The host owns normalized
events, Rumble snapshot conversion, local services and browser transport.

```mermaid
flowchart LR
  SB[Streamer.bot] <-->|WebSocket| H[ASP.NET Core host]
  R[Rumble HTTPS snapshots] --> H
  H <--> D[SQLite]
  H --> E[Browser editor]
  H --> O[OBS overlay runtime]
  H --> S[Speaker.bot]
```

## Build boundaries

- `ExtensionSuite.Core`: shared contracts and invariants. G01 supplies the
  polling-interval constraint; it does not implement the Rumble adapter.
- `ExtensionSuite.Data`: EF Core SQLite migrations and transactional repositories
  for events, checkpoints, outbox, non-secret configuration and redacted logs.
- `ExtensionSuite.StreamerBot` and `ExtensionSuite.Rumble`: independent adapters
  in G03/G04, referencing Core rather than frontend or each other.
- `ExtensionSuite.Finance`: idempotent ledger/valuation/identity strategies in G07.
- `ExtensionSuite.Overlays` and `ExtensionSuite.Web`: widget/transport/API services
  in their owning goals; empty build boundaries until implementation.
- `ExtensionSuite.Host`: composition root, HTTP editor assets, typed configuration,
  DPAPI provisioning, authenticated LAN, diagnostics, OpenAPI and editor WebSockets.
- `frontend/editor` and `frontend/overlay-runtime`: separate Vite build targets;
  editor-only dependencies must not enter the lightweight OBS runtime.

The foundation implements UUIDv7 events, transactional dedupe/checkpoint acceptance,
durable outbox, independent integration supervision and replay provenance. Actual
platform adapters remain G03/G04; the integration shell truthfully shows them disconnected.

The host serves the built editor; a separate Vite server supports development.
G05 supplies real OBS event transport; G06 supplies visual
editing. Integration placeholders must never claim actual connections.
