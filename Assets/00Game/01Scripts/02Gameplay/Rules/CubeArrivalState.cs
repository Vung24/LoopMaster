using System;

// Recovery helper for overlapping flights. Native Cube.PlayAddAnim snapshots size
// before its delay; arrival reservations preserve per-slice growth with landing callbacks.
public sealed class CubeArrivalState
{
    public int PendingCount { get; private set; }
    public int VisibleSize(int modelSize) => Math.Max(0, modelSize - PendingCount);
    public void Reserve() => PendingCount++;
    public bool Arrive()
    {
        if (PendingCount <= 0) return false;
        PendingCount--;
        return true;
    }
    public void Reset() => PendingCount = 0;
}
