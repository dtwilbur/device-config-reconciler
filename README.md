# Device Config Reconciler

Reconciles desired-state config events against a simulated fleet of unreliable devices.

## Run it

Requires .NET 8 SDK.

```
dotnet run --project src/DeviceReconciler.Api
```

API listens on the URL printed at startup (typically `http://localhost:5xxx`). SQLite db
file (`reconciler.db`) is created next to the API project on first run.

## Try it

macOS/Linux/bash:

```bash
curl -X POST http://localhost:5299/events \
  -H "Content-Type: application/json" \
  -d '{"deviceId":"dev-1","config":"{\"ssid\":\"guest\"}","version":1}'

curl http://localhost:5299/devices
curl http://localhost:5299/devices/dev-1
```

Windows PowerShell (its `curl` alias doesn't take bash flags, use `Invoke-RestMethod` instead):

```powershell
Invoke-RestMethod -Uri http://localhost:5299/events -Method Post -ContentType 'application/json' `
  -Body '{"deviceId":"dev-1","config":"{\"ssid\":\"guest\"}","version":1}'

Invoke-RestMethod http://localhost:5299/devices
Invoke-RestMethod http://localhost:5299/devices/dev-1
```

Push a higher version to see reconciliation kick in again, or replay the same/lower version
to see it get dropped as stale (check the console log).

## Tests

```
dotnet test
```

Covers: transient-error-then-success, retry exhaustion -> Failing, hard rejection ->
immediate Failing (no retry), stale version dropped, and one happy-path test.

## Assumptions

- `version` is a monotonically increasing integer per device; the caller "owns" it (no
  clock/UUID logic here).
- One event at a time is meaningful per device; the assignment doesn't require multi-tenant
  or ordering guarantees beyond version comparison.
- "Offline" status is modeled but nothing currently sets it, see DESIGN.md.
- Fault rates are hardcoded in `SimulatedDeviceSimulator` (70/15/10/5) as specified, seeded
  for reproducibility.

See [DESIGN.md](DESIGN.md) for the reasoning behind the above, and [AI_WORKFLOW.md](AI_WORKFLOW.md)
for how AI tools were used.
