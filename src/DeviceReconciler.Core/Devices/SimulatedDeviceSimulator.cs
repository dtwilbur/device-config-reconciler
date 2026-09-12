namespace DeviceReconciler.Core.Devices;

// Fakes the fleet. Fault rates are just hardcoded for now - could move to
// appsettings later if we need to tune them per env.
public class SimulatedDeviceSimulator : IDeviceSimulator
{
    private const double SuccessRate = 0.70;
    private const double TransientErrorRate = 0.15;
    private const double TimeoutRate = 0.10;
    // rest (0.05) is HardRejection

    private readonly Random _random;

    public SimulatedDeviceSimulator(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public async Task<PushResult> PushConfigAsync(string deviceId, string config, CancellationToken ct = default)
    {
        await Task.Delay(_random.Next(10, 50), ct); // fake some latency

        var roll = _random.NextDouble();
        if (roll < SuccessRate)
            return PushResult.Success;
        if (roll < SuccessRate + TransientErrorRate)
            return PushResult.TransientError;
        if (roll < SuccessRate + TransientErrorRate + TimeoutRate)
            return PushResult.Timeout;

        return PushResult.HardRejection;
    }
}
