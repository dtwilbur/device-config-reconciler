namespace DeviceReconciler.Core.Models;

// Config is just a raw JSON blob - we don't care what's in it, just push it through.
public record DesiredStateEvent(string DeviceId, string Config, long Version);
