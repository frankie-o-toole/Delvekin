using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns Water runtime state independently from the legacy Lava solver.
///
/// Authored water never moves or searches for exits by itself. Connected
/// WaterBody topology is rebuilt once after relevant voxel edits and remains
/// completely idle while the world is unchanged.
/// </summary>
public sealed class WaterSystem : IDisposable
{
    public const int MaximumAmount = (int)WaterAmount.Full;

    private static readonly Vector3Int[] CardinalDirections =
    {
        Vector3Int.forward,
        Vector3Int.right,
        Vector3Int.back,
        Vector3Int.left,
        Vector3Int.up,
        Vector3Int.down
    };

    private readonly VoxelWorld world;
    private readonly Dictionary<Vector3Int, WaterCell> cells = new();
    private readonly Dictionary<int, WaterBody> bodies = new();
    private readonly Dictionary<Vector3Int, int> bodyByPosition = new();

    private bool topologyDirty;
    private int nextBodyId = 1;

    public bool HasPendingWork => topologyDirty;
    public int BodyCount => bodies.Count;

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
        bodies.Clear();
        bodyByPosition.Clear();
        nextBodyId = 1;

        world.ForEachVoxel(
            (position, voxel) =>
            {
                if (voxel.Type == VoxelType.Water)
                {
                    cells[position] =
                        new WaterCell(WaterAmount.Full);
                }
            });

        topologyDirty = cells.Count > 0;
    }

    /// <summary>
    /// Resolves deferred topology work once. Multiple voxel edits performed
    /// in the same frame collapse into this single rebuild.
    /// </summary>
    public void ProcessPendingWork()
    {
        if (!topologyDirty)
        {
            return;
        }

        RebuildBodies();
        topologyDirty = false;
    }

    public bool TryGetCell(
        Vector3Int position,
        out WaterCell cell)
    {
        return cells.TryGetValue(position, out cell);
    }

    public bool TryGetBody(
        Vector3Int position,
        out WaterBody body)
    {
        body = null;

        return
            bodyByPosition.TryGetValue(position, out int bodyId) &&
            bodies.TryGetValue(bodyId, out body);
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
        topologyDirty = true;

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

    private void RebuildBodies()
    {
        bodies.Clear();
        bodyByPosition.Clear();

        HashSet<Vector3Int> unassigned = new(cells.Keys);
        Queue<Vector3Int> frontier = new();

        while (unassigned.Count > 0)
        {
            Vector3Int start = default;

            foreach (Vector3Int position in unassigned)
            {
                start = position;
                break;
            }

            WaterBody body = new(nextBodyId++);
            bodies.Add(body.Id, body);

            unassigned.Remove(start);
            frontier.Enqueue(start);

            while (frontier.Count > 0)
            {
                Vector3Int position = frontier.Dequeue();
                bodyByPosition[position] = body.Id;
                body.AddCell(position, GetAmount(position));

                foreach (Vector3Int direction in CardinalDirections)
                {
                    Vector3Int neighbour = position + direction;

                    if (!unassigned.Remove(neighbour))
                    {
                        continue;
                    }

                    frontier.Enqueue(neighbour);
                }
            }
        }
    }

    private bool TouchesWater(Vector3Int position)
    {
        if (cells.ContainsKey(position))
        {
            return true;
        }

        foreach (Vector3Int direction in CardinalDirections)
        {
            if (cells.ContainsKey(position + direction))
            {
                return true;
            }
        }

        return false;
    }

    private void HandleVoxelChanged(
        Vector3Int position,
        Voxel previous,
        Voxel current)
    {
        bool waterOccupancyChanged =
            previous.Type == VoxelType.Water ||
            current.Type == VoxelType.Water;

        if (previous.Type == VoxelType.Water)
        {
            cells.Remove(position);
        }

        if (current.Type == VoxelType.Water)
        {
            cells[position] =
                new WaterCell(WaterAmount.Full);
        }

        // Terrain edits matter only when they touch Water. This flag is
        // deferred, so a line or box edit still causes one body rebuild.
        if (waterOccupancyChanged || TouchesWater(position))
        {
            topologyDirty = true;
        }
    }
}
