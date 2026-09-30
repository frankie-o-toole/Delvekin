using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mutable scheduling state for one connected WaterBody. Simulation state is
/// deliberately separate from WaterBody's topology snapshot so independent
/// bodies can make progress within the same water tick.
/// </summary>
public sealed class WaterBodyRuntimeState
{
    private readonly HashSet<Vector3Int> wakePositions = new();

    public int BodyId { get; private set; }
    public bool IsActive { get; private set; }
    public int StableTickCount { get; private set; }
    public IReadOnlyCollection<Vector3Int> WakePositions => wakePositions;

    public WaterBodyRuntimeState(int bodyId)
    {
        BodyId = bodyId;
    }

    public void RemapTo(int bodyId)
    {
        BodyId = bodyId;
    }

    public void Wake(Vector3Int position)
    {
        wakePositions.Add(position);
        IsActive = true;
        StableTickCount = 0;
    }

    public void Wake(IEnumerable<Vector3Int> positions)
    {
        if (positions != null)
        {
            foreach (Vector3Int position in positions)
            {
                wakePositions.Add(position);
            }
        }

        IsActive = true;
        StableTickCount = 0;
    }

    public void RecordTick(bool transferredWater)
    {
        if (transferredWater)
        {
            IsActive = true;
            StableTickCount = 0;
            return;
        }

        StableTickCount++;
    }

    public void Sleep()
    {
        IsActive = false;
        StableTickCount = 0;
        wakePositions.Clear();
    }
}
