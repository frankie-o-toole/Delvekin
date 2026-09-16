using System;
using UnityEngine;

[Serializable]
public sealed class WaterSource
{
    public Vector3Int Position { get; }
    public int MaximumLevelY { get; }
    public int SupplyUnitsPerTick { get; }
    public Vector3Int InitialDirection { get; }

    public WaterSource(
        Vector3Int position,
        int maximumLevelY,
        int supplyUnitsPerTick,
        Vector3Int initialDirection)
    {
        Position = position;
        MaximumLevelY = maximumLevelY;
        SupplyUnitsPerTick = Mathf.Max(1, supplyUnitsPerTick);
        InitialDirection = initialDirection;
    }
}
