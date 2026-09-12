using DeviceReconciler.Core.Data;
using DeviceReconciler.Core.Reconciliation;

namespace DeviceReconciler.Api.Queue;

// Drains the queue, gives each event its own DI scope + DbContext, runs them
// concurrently (capped) so a device stuck retrying doesn't block the rest.
//
// TODO: two events for the same device could still race each other's
// read-modify-write of DeviceState - not handling that here, fine at this
// scale but would want to partition by deviceId with more time.
public class ReconciliationWorker : BackgroundService
{
    private const int MaxConcurrency = 20;

    private readonly EventQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReconciliationWorker> _logger;
    private readonly SemaphoreSlim _concurrencyLimiter = new(MaxConcurrency);

    public ReconciliationWorker(EventQueue queue, IServiceScopeFactory scopeFactory, ILogger<ReconciliationWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            await _concurrencyLimiter.WaitAsync(stoppingToken);
            _ = HandleEventAsync(evt, stoppingToken);
        }
    }

    private async Task HandleEventAsync(Core.Models.DesiredStateEvent evt, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ReconcilerDbContext>();
            var engine = scope.ServiceProvider.GetRequiredService<ReconciliationEngine>();
            await engine.ProcessEventAsync(db, evt, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unhandled error reconciling {DeviceId}", evt.DeviceId);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }
}
