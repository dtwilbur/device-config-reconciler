using DeviceReconciler.Api.Contracts;
using DeviceReconciler.Api.Queue;
using DeviceReconciler.Core.Data;
using DeviceReconciler.Core.Devices;
using DeviceReconciler.Core.Reconciliation;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=reconciler.db";
builder.Services.AddDbContext<ReconcilerDbContext>(o => o.UseSqlite(connectionString));

builder.Services.AddSingleton<EventQueue>();
builder.Services.AddSingleton<IDeviceSimulator>(_ => new SimulatedDeviceSimulator(seed: 42));
builder.Services.AddSingleton<IDelayProvider, RealDelayProvider>();
builder.Services.AddScoped<ReconciliationEngine>();
builder.Services.AddHostedService<ReconciliationWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReconcilerDbContext>();
    db.Database.EnsureCreated();
}

app.MapPost("/events", async (PostEventRequest req, EventQueue queue) =>
{
    var evt = new DeviceReconciler.Core.Models.DesiredStateEvent(req.DeviceId, req.Config, req.Version);
    await queue.Writer.WriteAsync(evt);
    return Results.Accepted(value: new { queued = true, evt.DeviceId, evt.Version });
});

app.MapGet("/devices", async (ReconcilerDbContext db) =>
{
    var devices = await db.Devices.OrderBy(d => d.DeviceId).ToListAsync();
    return Results.Ok(devices.Select(DeviceStatusResponse.From));
});

app.MapGet("/devices/{id}", async (string id, ReconcilerDbContext db) =>
{
    var device = await db.Devices.FindAsync(id);
    return device is null ? Results.NotFound() : Results.Ok(DeviceStatusResponse.From(device));
});

app.Run();

public partial class Program { } // for WebApplicationFactory<Program> in tests
