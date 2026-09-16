using System.Collections.Generic;
using UnityEngine;

public enum WaterBodyKind : byte
{
    Finite,
    SourceFed
}

public sealed class WaterBody
{
    private readonly HashSet<Vector3Int> cells = new();
    private readonly List<WaterSource> sources = new();

    public int Id { get; }
    public WaterBodyKind Kind =>
        sources.Count > 0 ? WaterBodyKind.SourceFed : WaterBodyKind.Finite;

    public int AmountUnits { get; internal set; }
    public int HighestCellY { get; internal set; }
    public int CellCount => cells.Count;
    public int SourceCount => sources.Count;
    public int MaximumSourceLevelY { get; private set; }
    public int TotalSourceUnitsPerTick { get; private set; }
    public IReadOnlyCollection<Vector3Int> Cells => cells;
    public IReadOnlyList<WaterSource> Sources => sources;

    internal WaterBody(int id)
    {
        Id = id;
        HighestCellY = int.MinValue;
        MaximumSourceLevelY = int.MinValue;
    }

    internal void AddCell(Vector3Int position, int amountUnits)
    {
        cells.Add(position);
        AmountUnits += amountUnits;
        HighestCellY = Mathf.Max(HighestCellY, position.y);
    }

    internal void AddSource(WaterSource source)
    {
        if (source == null)
        {
            return;
        }

        sources.Add(source);
        MaximumSourceLevelY =
            Mathf.Max(MaximumSourceLevelY, source.MaximumLevelY);
        TotalSourceUnitsPerTick += source.SupplyUnitsPerTick;
    }
}
