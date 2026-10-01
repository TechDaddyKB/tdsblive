# Foundation event contract

`CanonicalEvent` is the public normalization boundary. Identifiers are UUIDv7;
occurred/received timestamps use UTC. Routing fields (`source`, `platform`, `type`,
`nativeType`) and `dedupeKey` are required. User/message, native ID, monetary and
stream metadata are optional. Money uses integer minor units, never binary
floating point. Unknown raw fields remain after recursive credential redaction
unless raw retention is disabled. Known vault values are scrubbed across the
serialized envelope before persistence or editor delivery.

Provenance is `live`, `simulation` or `replay`. Dedupe is unique on
`(source, provenance, dedupeKey)`; a test cannot suppress a live event. Event,
checkpoint and outbox writes share one SQLite transaction. A duplicate advances
its checkpoint without creating another event/outbox row. Failed checkpoint
writes roll back the entire acceptance. WAL and foreign keys are enabled;
versioned EF migrations run before the HTTP listener starts.

`AcceptAsync` declines simulation/replay persistence unless explicitly requested.
Ordinary history reads return live events only. `/api/test-event` forces simulation
and never permits live action execution, regardless of submitted provenance.
Unpersisted simulations can reach the editor but do not touch the durable tables.

The initial durable outbox delivers to the editor event hub in batches of 64.
Delivery means handoff to the bounded internal subscriber queues, not proof that
a browser rendered or acknowledged an event. Event history remains available when
there are no subscribers. A crash between publication and the delivery marker may
repeat an event; consumers must dedupe by UUID. Reconnect clients resubscribe and
reload history through REST. This does not guarantee exactly-once external actions.
G03/G04 add real adapters and bridge execution records; G07 owns financial writes.

The integration service boundary runs each registered adapter independently,
records running/degraded/stopped state, and retries failure after five seconds.
No live integration is registered by G02. Outbox failure leaves durable rows
pending and retries independently. Shutdown cancels socket sessions/workers before
the final WAL checkpoint; checkpointing is idempotent under overlapping stop calls.
