using System;
using UnityEngine;

/// <summary>
/// Authored metadata for an infinite water inlet. SourceLevel is an absolute
/// voxel Y coordinate; FlowRate is measured in half-water units per future
/// simulation tick.
/// </summary>
[Serializable]
public readonly struct WaterSourceData
{
    public readonly Vector3Int Position;
    public readonly int SourceLevel;
    public readonly int FlowRate;
    public readonly Vector3Int InitialDirection;

    public WaterSourceData(
        Vector3Int position,
        int sourceLevel,
        int flowRate,
        Vector3Int initialDirection)
    {
        Position = position;
        SourceLevel = sourceLevel;
        FlowRate = Mathf.Max(1, flowRate);
        InitialDirection = IsCardinalOrDown(initialDirection)
            ? initialDirection
            : Vector3Int.zero;
    }

    private static bool IsCardinalOrDown(Vector3Int direction)
    {
        if (direction == Vector3Int.down)
        {
            return true;
        }

        return direction.y == 0 &&
               Mathf.Abs(direction.x) + Mathf.Abs(direction.z) == 1;
    }
}
