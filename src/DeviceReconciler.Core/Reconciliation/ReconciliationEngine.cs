using DeviceReconciler.Core.Data;
using DeviceReconciler.Core.Devices;
using DeviceReconciler.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeviceReconciler.Core.Reconciliation;

// Version check -> push -> retry w/ backoff -> give up. One event start to
// finish per call, blocks through the backoff delays - caller (the worker)
// fans these out so a slow device doesn't hold up everyone else.
public class ReconciliationEngine
{
    public const int MaxAttempts = 5;
    private static readonly TimeSpan BaseBackoff = TimeSpan.FromSeconds(1);

    private readonly IDeviceSimulator _simulator;
    private readonly IDelayProvider _delay;
    private readonly ILogger<ReconciliationEngine> _logger;

    public ReconciliationEngine(IDeviceSimulator simulator, IDelayProvider delay, ILogger<ReconciliationEngine> logger)
    {
        _simulator = simulator;
        _delay = delay;
        _logger = logger;
    }

    public async Task ProcessEventAsync(ReconcilerDbContext db, DesiredStateEvent evt, CancellationToken ct = default)
    {
        var device = await db.Devices.FindAsync(new object[] { evt.DeviceId }, ct)
            ?? new DeviceState { DeviceId = evt.DeviceId };

        if (device.AppliedVersion >= evt.Version && device.Status != DeviceStatus.Unknown)
        {
            _logger.LogInformation(
                "Dropping stale event for {DeviceId}: incoming version {Incoming} <= applied {Applied}",
                evt.DeviceId, evt.Version, device.AppliedVersion);
            return;
        }

        if (db.Entry(device).State == EntityState.Detached)
            db.Devices.Add(device);

        device.PendingVersion = evt.Version;
        device.Status = DeviceStatus.Drifting;
        device.AttemptCount = 0;
        await db.SaveChangesAsync(ct);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            device.AttemptCount = attempt;
            var result = await _simulator.PushConfigAsync(evt.DeviceId, evt.Config, ct);

            switch (result)
            {
                case PushResult.Success:
                    device.Status = DeviceStatus.Synced;
                    device.AppliedVersion = evt.Version;
                    device.AppliedConfig = evt.Config;
                    device.PendingVersion = null;
                    device.LastError = null;
                    device.LastUpdatedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Device {DeviceId} synced to version {Version}", evt.DeviceId, evt.Version);
                    return;

                case PushResult.HardRejection:
                    device.Status = DeviceStatus.Failing;
                    device.LastError = "Hard rejection from device";
                    device.LastUpdatedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(ct);
                    _logger.LogWarning("Device {DeviceId} hard-rejected version {Version}, giving up", evt.DeviceId, evt.Version);
                    return;

                case PushResult.TransientError:
                case PushResult.Timeout:
                    device.LastError = result.ToString();
                    device.LastUpdatedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(ct);

                    if (attempt == MaxAttempts)
                    {
                        device.Status = DeviceStatus.Failing;
                        await db.SaveChangesAsync(ct);
                        _logger.LogWarning(
                            "Device {DeviceId} exhausted {MaxAttempts} attempts on version {Version}, marking Failing",
                            evt.DeviceId, MaxAttempts, evt.Version);
                        return;
                    }

                    var backoff = TimeSpan.FromMilliseconds(BaseBackoff.TotalMilliseconds * Math.Pow(2, attempt - 1));
                    _logger.LogInformation(
                        "Device {DeviceId} attempt {Attempt} failed ({Result}), retrying in {Backoff}",
                        evt.DeviceId, attempt, result, backoff);
                    await _delay.Delay(backoff, ct);
                    break;
            }
        }
    }
}
