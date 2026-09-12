namespace DeviceReconciler.Core.Models;

public enum DeviceStatus
{
    Unknown, // no event applied yet
    Synced,
    Drifting, // push in flight / retrying
    Failing,  // gave up - retries exhausted or hard rejection
    Offline   // TODO: nothing sets this yet, would need a heartbeat check
}
