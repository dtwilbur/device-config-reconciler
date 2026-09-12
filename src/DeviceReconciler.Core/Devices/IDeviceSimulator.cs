namespace DeviceReconciler.Core.Devices;

public enum PushResult
{
    Success,
    TransientError,
    Timeout,
    HardRejection
}

public interface IDeviceSimulator
{
    Task<PushResult> PushConfigAsync(string deviceId, string config, CancellationToken ct = default);
}
