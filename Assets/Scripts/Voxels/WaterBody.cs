using System.Collections.Generic;
using UnityEngine;

public enum WaterBodyKind : byte
{
    Finite,
    SourceFed
}

/// <summary>
/// Cached topology for one connected group of Water voxels.
/// A body is data, not an active simulation. It is rebuilt only after
/// occupancy or neighbouring terrain changes.
/// </summary>
public sealed class WaterBody
{
    private readonly HashSet<Vector3Int> cells = new();

    public int Id { get; }
    public WaterBodyKind Kind { get; internal set; }
    public int AmountUnits { get; internal set; }
    public int HighestCellY { get; internal set; }
    public int CellCount => cells.Count;
    public IReadOnlyCollection<Vector3Int> Cells => cells;

    internal WaterBody(int id)
    {
        Id = id;
        Kind = WaterBodyKind.Finite;
        HighestCellY = int.MinValue;
    }

    internal void AddCell(
        Vector3Int position,
        int amountUnits)
    {
        cells.Add(position);
        AmountUnits += amountUnits;
        HighestCellY = Mathf.Max(HighestCellY, position.y);
    }
}
