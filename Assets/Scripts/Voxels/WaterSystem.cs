using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns Water runtime state independently from the legacy Lava solver.
/// Water is simulated only while cells are awake. Stable water has no
/// per-frame search cost.
/// </summary>
public sealed class WaterSystem : IDisposable
{
    public const int MaximumAmount = (int)WaterAmount.Full;

    private const float TickInterval = 0.1f;
    private const int MaximumStepsPerFrame = 4;

    private static readonly Vector3Int[] HorizontalDirections =
    {
        Vector3Int.forward,
        Vector3Int.right,
        Vector3Int.back,
        Vector3Int.left
    };

    private readonly VoxelWorld world;
    private readonly Dictionary<Vector3Int, WaterCell> cells = new();
    private readonly Queue<Vector3Int> awakeCells = new();
    private readonly HashSet<Vector3Int> queuedCells = new();

    private float tickAccumulator;
    private bool applyingWaterEdit;

    public bool HasAwakeWater => awakeCells.Count > 0;

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
        awakeCells.Clear();
        queuedCells.Clear();
        tickAccumulator = 0f;

        world.ForEachVoxel(
            (position, voxel) =>
            {
                if (voxel.Type == VoxelType.Water)
                {
                    cells[position] =
                        new WaterCell(WaterAmount.Full);
                }
            });
    }

    public void Tick(float deltaTime)
    {
        if (!HasAwakeWater)
        {
            tickAccumulator = 0f;
            return;
        }

        tickAccumulator += deltaTime;
        int steps = 0;

        while (tickAccumulator >= TickInterval &&
               steps < MaximumStepsPerFrame &&
               HasAwakeWater)
        {
            tickAccumulator -= TickInterval;
            SimulateAwakePass();
            steps++;
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
        WakeNeighborhood(position);

        if (refreshVisuals)
        {
            world.RefreshVoxelVisuals(new[] { position });
        }

        return true;
    }

    public int SetAmounts(
        IEnumerable<Vector3Int> positions,
        WaterAmount amount)
    {
        if (positions == null)
        {
            return 0;
        }

        List<Vector3Int> changed = new();

        foreach (Vector3Int position in positions)
        {
            if (SetAmount(position, amount, refreshVisuals: false))
            {
                changed.Add(position);
            }
        }

        if (changed.Count > 0)
        {
            world.RefreshVoxelVisuals(changed);
        }

        return changed.Count;
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

    private void SimulateAwakePass()
    {
        int passCount = awakeCells.Count;

        for (int index = 0; index < passCount; index++)
        {
            Vector3Int position = awakeCells.Dequeue();
            queuedCells.Remove(position);

            if (!cells.ContainsKey(position))
            {
                continue;
            }

            TryMoveWater(position);
        }
    }

    private bool TryMoveWater(Vector3Int source)
    {
        Vector3Int below = source + Vector3Int.down;

        if (TryTransferDown(source, below))
        {
            return true;
        }

        int directionOffset =
            Mathf.Abs(
                source.x * 73856093 ^
                source.y * 19349663 ^
                source.z * 83492791) %
            HorizontalDirections.Length;

        // Prefer a horizontal cell that immediately leads downward.
        for (int index = 0;
             index < HorizontalDirections.Length;
             index++)
        {
            Vector3Int direction =
                HorizontalDirections[
                    (index + directionOffset) %
                    HorizontalDirections.Length];

            Vector3Int target = source + direction;

            if (CanDrainFrom(target) &&
                TryTransfer(
                    source,
                    target,
                    GetAmount(source)))
            {
                return true;
            }
        }

        // A body may create a half-height shore on supported ground.
        // A single isolated voxel deliberately does not split outward.
        if (GetAmount(source) == MaximumAmount &&
            HasCardinalWaterNeighbour(source))
        {
            for (int index = 0;
                 index < HorizontalDirections.Length;
                 index++)
            {
                Vector3Int direction =
                    HorizontalDirections[
                        (index + directionOffset) %
                        HorizontalDirections.Length];

                Vector3Int target = source + direction;

                if (CanReceiveShoreWater(target) &&
                    TryTransfer(source, target, 1))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryTransferDown(
        Vector3Int source,
        Vector3Int target)
    {
        if (!world.ContainsExistingChunkAt(target))
        {
            return false;
        }

        VoxelType targetType = world.GetVoxel(target).Type;

        if (targetType == VoxelType.Air)
        {
            return TryTransfer(
                source,
                target,
                GetAmount(source));
        }

        if (targetType != VoxelType.Water)
        {
            return false;
        }

        int capacity = MaximumAmount - GetAmount(target);

        return capacity > 0 &&
               TryTransfer(
                   source,
                   target,
                   Mathf.Min(GetAmount(source), capacity));
    }

    private bool CanDrainFrom(Vector3Int position)
    {
        if (!world.ContainsExistingChunkAt(position) ||
            world.GetVoxel(position).Type != VoxelType.Air)
        {
            return false;
        }

        Vector3Int below = position + Vector3Int.down;

        if (!world.ContainsExistingChunkAt(below))
        {
            return false;
        }

        VoxelType belowType = world.GetVoxel(below).Type;

        return belowType == VoxelType.Air ||
               (belowType == VoxelType.Water &&
                GetAmount(below) < MaximumAmount);
    }

    private bool CanReceiveShoreWater(Vector3Int position)
    {
        if (!world.ContainsExistingChunkAt(position) ||
            world.GetVoxel(position).Type != VoxelType.Air)
        {
            return false;
        }

        Vector3Int below = position + Vector3Int.down;

        return world.ContainsExistingChunkAt(below) &&
               world.GetVoxel(below).Type != VoxelType.Air &&
               world.GetVoxel(below).Type != VoxelType.Water;
    }

    private bool HasCardinalWaterNeighbour(Vector3Int position)
    {
        foreach (Vector3Int direction in HorizontalDirections)
        {
            if (cells.ContainsKey(position + direction))
            {
                return true;
            }
        }

        return cells.ContainsKey(position + Vector3Int.up) ||
               cells.ContainsKey(position + Vector3Int.down);
    }

    private bool TryTransfer(
        Vector3Int source,
        Vector3Int target,
        int requestedAmount)
    {
        int sourceAmount = GetAmount(source);

        if (sourceAmount <= 0 ||
            requestedAmount <= 0 ||
            !world.ContainsExistingChunkAt(target))
        {
            return false;
        }

        VoxelType targetType = world.GetVoxel(target).Type;

        if (targetType != VoxelType.Air &&
            targetType != VoxelType.Water)
        {
            return false;
        }

        int targetAmount = GetAmount(target);
        int transferAmount = Mathf.Min(
            requestedAmount,
            Mathf.Min(
                sourceAmount,
                MaximumAmount - targetAmount));

        if (transferAmount <= 0)
        {
            return false;
        }

        int remainingSource = sourceAmount - transferAmount;
        int resultingTarget = targetAmount + transferAmount;

        applyingWaterEdit = true;

        try
        {
            Dictionary<Vector3Int, Voxel> voxelChanges = new();

            if (remainingSource == 0)
            {
                cells.Remove(source);
                voxelChanges[source] =
                    new Voxel(VoxelType.Air);
            }
            else
            {
                WaterCell sourceCell = cells[source];
                sourceCell.Amount =
                    (WaterAmount)remainingSource;
                cells[source] = sourceCell;
            }

            if (targetAmount == 0)
            {
                cells[target] =
                    new WaterCell(
                        (WaterAmount)resultingTarget);
                voxelChanges[target] =
                    new Voxel(VoxelType.Water);
            }
            else
            {
                WaterCell targetCell = cells[target];
                targetCell.Amount =
                    (WaterAmount)resultingTarget;
                cells[target] = targetCell;
            }

            if (voxelChanges.Count > 0)
            {
                world.SetFluidVoxelStates(voxelChanges);
            }
        }
        finally
        {
            applyingWaterEdit = false;
        }

        WakeNeighborhood(source);
        WakeNeighborhood(target);

        world.RefreshVoxelVisuals(
            new[]
            {
                source,
                target
            });

        return true;
    }

    private void WakeNeighborhood(Vector3Int position)
    {
        WakeIfWater(position);
        WakeIfWater(position + Vector3Int.up);
        WakeIfWater(position + Vector3Int.down);

        foreach (Vector3Int direction in HorizontalDirections)
        {
            WakeIfWater(position + direction);
        }
    }

    private void WakeIfWater(Vector3Int position)
    {
        if (!cells.ContainsKey(position) ||
            !queuedCells.Add(position))
        {
            return;
        }

        awakeCells.Enqueue(position);
    }

    private void HandleVoxelChanged(
        Vector3Int position,
        Voxel previous,
        Voxel current)
    {
        if (applyingWaterEdit)
        {
            return;
        }

        if (previous.Type == VoxelType.Water)
        {
            cells.Remove(position);
            queuedCells.Remove(position);
        }

        if (current.Type == VoxelType.Water)
        {
            cells[position] =
                new WaterCell(WaterAmount.Full);
        }

        WakeNeighborhood(position);
    }
}
