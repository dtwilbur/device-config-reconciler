# Design Notes

## Key decisions

**In-process Channel instead of a real broker.** Assignment allows it, and ~100 devices doesn't
need Rabbit/SQS. Downside: an event is gone if the process crashes between POST and the worker
picking it up. Would swap this for a durable queue if it needed to survive a crash mid-flight.

**Retry loop blocks through backoff instead of re-enqueueing.** `ReconciliationEngine.ProcessEventAsync`
just does `Task.Delay` inline for backoff rather than scheduling itself again later. Easier to
follow and test (swap in a no-op `IDelayProvider`), but it ties up a worker slot for the whole
retry lifetime of a device. Capped concurrency (semaphore, 20) in `ReconciliationWorker` so one
device stuck retrying doesn't stall the rest. Real gap: a restart mid-backoff loses that retry's
progress. Final synced/failing state is still on disk, just not the in-flight attempt.

**No locking across concurrent events for the same device.** Two events for one device could race
on the read-modify-write of `DeviceState`. Didn't handle it, it's a TODO in the worker. Not a
problem at this scale, would need to be if this saw real traffic (partition by deviceId, or an
optimistic concurrency token).

**Stale version just gets dropped.** `incoming <= applied` means someone already got there first,
or a newer one is already in. No attempt to detect/reorder out-of-order delivery. Logged so it's
not silent, but there's no "rejected events" table or anything like that.

**Status: Synced / Drifting / Failing / Offline / Unknown.** Drifting = actively retrying, Failing
= gave up (retries exhausted or hard rejection), needs a new event to clear it. Offline exists in
the enum but nothing sets it, there's no heartbeat, only push results. Would need some kind of
liveness signal (or "N consecutive timeouts" heuristic) to tell "device unreachable" apart from
"config rejected."

**Tests use real Sqlite `:memory:`, not the EF InMemory provider.** EF's InMemory provider skips
real SQL semantics and can pass things that'd fail on a real DB. Sqlite `:memory:` costs basically
nothing extra and actually exercises the code path.

## What I'd change with more time

- Persist retry/backoff state so it survives a restart, not just the final device status
- Per-device serialization so concurrent events for the same device can't race
- Real Offline detection instead of an unused enum value
- Some kind of history/timeline per device instead of just current-state snapshot, better for
  an actual on-call debugging session
- Input validation on POST /events (currently none, per the scope note to skip validation middleware)
- Idempotency key in case the upstream retries a POST

See [AI_WORKFLOW.md](AI_WORKFLOW.md) for the AI workflow note.
