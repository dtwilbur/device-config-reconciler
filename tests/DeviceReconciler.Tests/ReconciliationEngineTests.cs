using DeviceReconciler.Core.Data;
using DeviceReconciler.Core.Devices;
using DeviceReconciler.Core.Models;
using DeviceReconciler.Core.Reconciliation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;
using Moq;
using Xunit;

namespace DeviceReconciler.Tests;

public class ReconciliationEngineTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ReconcilerDbContext _db;

    public ReconciliationEngineTests()
    {
        // Sqlite in-memory needs a single open connection kept alive for the
        // db's lifetime, otherwise the "database" disappears between calls.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ReconcilerDbContext>().UseSqlite(_connection).Options;
        _db = new ReconcilerDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static ReconciliationEngine BuildEngine(IDeviceSimulator simulator, IDelayProvider? delay = null) =>
        new(simulator, delay ?? new NoOpDelayProvider(), NullLogger<ReconciliationEngine>.Instance);

    [Fact]
    public async Task TransientError_ThenSuccess_EndsSynced()
    {
        var simulator = new Mock<IDeviceSimulator>();
        simulator.SetupSequence(s => s.PushConfigAsync("dev-1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PushResult.TransientError)
            .ReturnsAsync(PushResult.Success);

        var engine = BuildEngine(simulator.Object);
        var evt = new DesiredStateEvent("dev-1", "{}", 1);

        await engine.ProcessEventAsync(_db, evt);

        var device = await _db.Devices.FindAsync("dev-1");
        Assert.NotNull(device);
        Assert.Equal(DeviceStatus.Synced, device!.Status);
        Assert.Equal(1, device.AppliedVersion);
        Assert.Equal(2, device.AttemptCount);
        simulator.Verify(s => s.PushConfigAsync("dev-1", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task TransientError_ExhaustsRetries_EndsFailing()
    {
        var simulator = new Mock<IDeviceSimulator>();
        simulator.Setup(s => s.PushConfigAsync("dev-2", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PushResult.TransientError);

        var engine = BuildEngine(simulator.Object);
        var evt = new DesiredStateEvent("dev-2", "{}", 1);

        await engine.ProcessEventAsync(_db, evt);

        var device = await _db.Devices.FindAsync("dev-2");
        Assert.NotNull(device);
        Assert.Equal(DeviceStatus.Failing, device!.Status);
        Assert.Equal(0, device.AppliedVersion);
        Assert.Equal(ReconciliationEngine.MaxAttempts, device.AttemptCount);
        simulator.Verify(
            s => s.PushConfigAsync("dev-2", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(ReconciliationEngine.MaxAttempts));
    }

    [Fact]
    public async Task HardRejection_FailsImmediately_NoRetry()
    {
        var simulator = new Mock<IDeviceSimulator>();
        simulator.Setup(s => s.PushConfigAsync("dev-3", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PushResult.HardRejection);

        var engine = BuildEngine(simulator.Object);
        var evt = new DesiredStateEvent("dev-3", "{}", 1);

        await engine.ProcessEventAsync(_db, evt);

        var device = await _db.Devices.FindAsync("dev-3");
        Assert.NotNull(device);
        Assert.Equal(DeviceStatus.Failing, device!.Status);
        Assert.Equal("Hard rejection from device", device.LastError);
        simulator.Verify(s => s.PushConfigAsync("dev-3", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StaleVersion_IsDropped_DeviceUnchanged()
    {
        var simulator = new Mock<IDeviceSimulator>();
        simulator.Setup(s => s.PushConfigAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PushResult.Success);

        var engine = BuildEngine(simulator.Object);

        // First bring the device to Synced@version 5.
        await engine.ProcessEventAsync(_db, new DesiredStateEvent("dev-4", "{}", 5));
        simulator.Invocations.Clear();

        // Now replay an older/equal version.
        await engine.ProcessEventAsync(_db, new DesiredStateEvent("dev-4", "{\"stale\":true}", 5));

        var device = await _db.Devices.FindAsync("dev-4");
        Assert.NotNull(device);
        Assert.Equal(5, device!.AppliedVersion);
        Assert.Equal(DeviceStatus.Synced, device.Status);
        simulator.Verify(
            s => s.PushConfigAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HappyPath_FirstEvent_Syncs()
    {
        var simulator = new Mock<IDeviceSimulator>();
        simulator.Setup(s => s.PushConfigAsync("dev-5", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PushResult.Success);

        var engine = BuildEngine(simulator.Object);
        await engine.ProcessEventAsync(_db, new DesiredStateEvent("dev-5", "{\"a\":1}", 1));

        var device = await _db.Devices.FindAsync("dev-5");
        Assert.NotNull(device);
        Assert.Equal(DeviceStatus.Synced, device!.Status);
        Assert.Equal(1, device.AppliedVersion);
        Assert.Equal(1, device.AttemptCount);
    }

    private class NoOpDelayProvider : IDelayProvider
    {
        public Task Delay(TimeSpan delay, CancellationToken ct = default) => Task.CompletedTask;
    }
}
