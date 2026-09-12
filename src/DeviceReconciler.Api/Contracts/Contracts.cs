using DeviceReconciler.Core.Models;

namespace DeviceReconciler.Api.Contracts;

public record PostEventRequest(string DeviceId, string Config, long Version);

public record DeviceStatusResponse(
    string DeviceId,
    DeviceStatus Status,
    long AppliedVersion,
    long? PendingVersion,
    int AttemptCount,
    string? LastError,
    DateTimeOffset? LastUpdatedAt)
{
    public static DeviceStatusResponse From(DeviceState d) => new(
        d.DeviceId, d.Status, d.AppliedVersion, d.PendingVersion, d.AttemptCount, d.LastError, d.LastUpdatedAt);
}
