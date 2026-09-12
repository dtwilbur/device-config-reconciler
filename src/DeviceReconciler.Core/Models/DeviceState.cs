namespace DeviceReconciler.Core.Models;

public class DeviceState
{
    public string DeviceId { get; set; } = "";
    public DeviceStatus Status { get; set; } = DeviceStatus.Unknown;

    public long AppliedVersion { get; set; }
    public string? AppliedConfig { get; set; }

    public long? PendingVersion { get; set; } // null when nothing in flight

    public int AttemptCount { get; set; }
    public string? LastError { get; set; }

    public DateTimeOffset? LastUpdatedAt { get; set; }
}
