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
    private readonly Dictionary<Vector3Int, WaterSourceData> sources = new();

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

    public void ResetFromWorld(SavedLevel savedLevel = null)
    {
        cells.Clear();
        sources.Clear();

        world.ForEachVoxel(
            (position, voxel) =>
            {
                if (voxel.Type == VoxelType.Water)
                {
                    cells[position] = new WaterCell(WaterAmount.Full);
                }
            });

        if (savedLevel == null)
        {
            return;
        }

        if (savedLevel.voxels != null)
        {
            foreach (SavedVoxel savedVoxel in savedLevel.voxels)
            {
                if (savedVoxel.type != VoxelType.Water)
                {
                    continue;
                }

                Vector3Int position = new(
                    savedVoxel.x,
                    savedVoxel.y,
                    savedVoxel.z);

                // Version-one saves have no waterAmount field and deserialize
                // it as zero. Treat that legacy value as Full.
                WaterAmount amount = savedVoxel.waterAmount ==
                                     (byte)WaterAmount.Half
                    ? WaterAmount.Half
                    : WaterAmount.Full;

                if (cells.ContainsKey(position))
                {
                    cells[position] = new WaterCell(amount);
                }
            }
        }

        if (savedLevel.waterSources == null)
        {
            return;
        }

        foreach (SavedWaterSource savedSource in savedLevel.waterSources)
        {
            WaterSourceData source = savedSource.ToRuntime();

            if (cells.ContainsKey(source.Position))
            {
                sources[source.Position] = source;
            }
        }
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

    public bool SetAmount(
        Vector3Int position,
        WaterAmount amount,
        bool refreshVisuals = true)
    {
        if (world.GetVoxel(position).Type != VoxelType.Water ||
            !cells.TryGetValue(position, out WaterCell cell))
        {
            return false;
        }

        if (cell.Amount == amount)
        {
            return false;
        }

        cell.Amount = amount;
        cells[position] = cell;

        if (refreshVisuals)
        {
            world.RefreshVoxelVisuals(new[] { position });
        }

        return true;
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

    public IEnumerable<WaterSourceData> GetSources()
    {
        return sources.Values;
    }

    public bool TryGetSource(
        Vector3Int position,
        out WaterSourceData source)
    {
        return sources.TryGetValue(position, out source);
    }

    public bool SetSource(WaterSourceData source)
    {
        if (world.GetVoxel(source.Position).Type != VoxelType.Water)
        {
            return false;
        }

        sources[source.Position] = source;
        return true;
    }

    public bool RemoveSource(Vector3Int position)
    {
        return sources.Remove(position);
    }

    private void HandleVoxelChanged(
        Vector3Int position,
        Voxel previous,
        Voxel current)
    {
        if (previous.Type == VoxelType.Water)
        {
            cells.Remove(position);
            sources.Remove(position);
        }

        if (current.Type == VoxelType.Water)
        {
            cells[position] = new WaterCell(WaterAmount.Full);
        }
    }
}
