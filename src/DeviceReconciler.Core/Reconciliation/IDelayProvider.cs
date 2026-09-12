namespace DeviceReconciler.Core.Reconciliation;

// Exists so tests can skip real backoff delays without touching the retry logic.
public interface IDelayProvider
{
    Task Delay(TimeSpan delay, CancellationToken ct = default);
}

public class RealDelayProvider : IDelayProvider
{
    public Task Delay(TimeSpan delay, CancellationToken ct = default) => Task.Delay(delay, ct);
}
