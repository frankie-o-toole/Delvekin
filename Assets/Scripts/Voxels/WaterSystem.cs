using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns Water runtime state independently from the legacy Lava solver.
/// Phase one deliberately performs no water movement: authored Water voxels
/// load as full, still cells and remain asleep until later body/source solvers
/// are introduced.
/// </summary>
public sealed class WaterSystem : IDisposable
{
    public const int MaximumAmount = (int)WaterAmount.Full;

    private readonly VoxelWorld world;
    private readonly Dictionary<Vector3Int, WaterCell> cells = new();

    public WaterSystem(VoxelWorld world)
    {
        this.world = world;
        world.VoxelChanged += HandleVoxelChanged;
    }

    public void Dispose()
    {
        if (world != null)
        {
            world.VoxelChanged -= HandleVoxelChanged;
        }
    }

    public void ResetFromWorld()
    {
        cells.Clear();

        world.ForEachVoxel(
            (position, voxel) =>
            {
                if (voxel.Type == VoxelType.Water)
                {
                    cells[position] = new WaterCell(WaterAmount.Full);
                }
            });
    }

    public bool TryGetCell(
        Vector3Int position,
        out WaterCell cell)
    {
        return cells.TryGetValue(position, out cell);
    }

    public int GetAmount(Vector3Int position)
    {
        return cells.TryGetValue(position, out WaterCell cell)
            ? (int)cell.Amount
            : 0;
    }

    public float GetFill01(Vector3Int position)
    {
        return GetAmount(position) / (float)MaximumAmount;
    }

    public WaterMotion GetMotion(Vector3Int position)
    {
        return cells.TryGetValue(position, out WaterCell cell)
            ? cell.Motion
            : WaterMotion.Still;
    }

    public Vector3Int GetPrimaryFlowDirection(Vector3Int position)
    {
        return cells.TryGetValue(position, out WaterCell cell)
            ? cell.PrimaryFlowDirection
            : Vector3Int.zero;
    }

    private void HandleVoxelChanged(
        Vector3Int position,
        Voxel previous,
        Voxel current)
    {
        if (previous.Type == VoxelType.Water)
        {
            cells.Remove(position);
        }

        if (current.Type == VoxelType.Water)
        {
            cells[position] = new WaterCell(WaterAmount.Full);
        }
    }
}
