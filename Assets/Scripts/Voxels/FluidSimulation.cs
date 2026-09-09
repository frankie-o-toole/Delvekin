using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FluidTuning
{
    [Min(0.02f)]
    public float tickInterval = 0.15f;

    [Range(1, 8)]
    public int maximumDownwardTransfer = 8;

    [Range(1, 8)]
    public int maximumHorizontalTransfer = 4;

    [Range(0, 4)]
    public int minimumHorizontalDifference = 1;

    [Range(1, 64)]
    public int drainSearchDistance = 24;
}

/// <summary>
/// Conserved, grid-based fluid simulation shared by Water and Lava.
/// Fluid amounts are runtime state: authored Water/Lava voxels begin full
/// whenever a level is loaded.
/// </summary>
public sealed class FluidSimulation : IDisposable
{
    public const int MaximumAmount = 8;

    private static readonly Vector3Int[] HorizontalDirections =
    {
        Vector3Int.right,
        Vector3Int.forward,
        Vector3Int.left,
        Vector3Int.back
    };

    private readonly VoxelWorld world;
    private readonly FluidTuning waterTuning;
    private readonly FluidTuning lavaTuning;

    private readonly Dictionary<Vector3Int, FluidCell> cells = new();
    private readonly HashSet<Vector3Int> activeWater = new();
    private readonly HashSet<Vector3Int> activeLava = new();

    private float waterElapsed;
    private float lavaElapsed;
    private int tickIndex;
    private bool applyingSimulationChanges;

    private struct FluidCell
    {
        public VoxelType Type;
        public int Amount;
        public Vector3Int FlowDirection;

        public FluidCell(VoxelType type, int amount)
        {
            Type = type;
            Amount = amount;
            FlowDirection = Vector3Int.zero;
        }
    }

    public FluidSimulation(
        VoxelWorld world,
        FluidTuning waterTuning,
        FluidTuning lavaTuning)
    {
        this.world = world;
        this.waterTuning = waterTuning;
        this.lavaTuning = lavaTuning;

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
        activeWater.Clear();
        activeLava.Clear();

        waterElapsed = 0f;
        lavaElapsed = 0f;
        tickIndex = 0;

        world.ForEachVoxel(
            (position, voxel) =>
            {
                if (!IsFluid(voxel.Type))
                {
                    return;
                }

                cells[position] =
                    new FluidCell(voxel.Type, MaximumAmount);

                GetActiveSet(voxel.Type).Add(position);
            });
    }

    public void Tick(float deltaTime)
    {
        waterElapsed += deltaTime;
        lavaElapsed += deltaTime;

        RunDueTicks(
            VoxelType.Water,
            waterTuning,
            ref waterElapsed);

        RunDueTicks(
            VoxelType.Lava,
            lavaTuning,
            ref lavaElapsed);
    }

    public int GetAmount(Vector3Int position)
    {
        return cells.TryGetValue(position, out FluidCell cell)
            ? cell.Amount
            : 0;
    }

    public Vector3Int GetFlowDirection(Vector3Int position)
    {
        return cells.TryGetValue(position, out FluidCell cell)
            ? cell.FlowDirection
            : Vector3Int.zero;
    }

    private void RunDueTicks(
        VoxelType type,
        FluidTuning tuning,
        ref float elapsed)
    {
        float interval = Mathf.Max(0.02f, tuning.tickInterval);

        // The cap prevents a long hitch from causing an even larger catch-up
        // hitch. Remaining elapsed time is retained for later frames.
        int ticksThisFrame = 0;

        while (elapsed >= interval && ticksThisFrame < 4)
        {
            elapsed -= interval;
            SimulateType(type, tuning);
            ticksThisFrame++;
        }
    }

    private void SimulateType(
        VoxelType type,
        FluidTuning tuning)
    {
        HashSet<Vector3Int> active = GetActiveSet(type);

        if (active.Count == 0)
        {
            return;
        }

        Dictionary<Vector3Int, FluidCell> working =
            new(cells);

        List<Vector3Int> sources = new(active);
        sources.Sort(ComparePositions);

        HashSet<Vector3Int> changed = new();

        foreach (Vector3Int source in sources)
        {
            if (!cells.TryGetValue(source, out FluidCell snapshotCell) ||
                snapshotCell.Type != type ||
                snapshotCell.Amount <= 0 ||
                !working.TryGetValue(source, out FluidCell sourceCell))
            {
                continue;
            }

            // Current describes recent movement. A cell that does not move
            // again settles back to zero on the next simulation tick.
            if (sourceCell.FlowDirection != Vector3Int.zero)
            {
                sourceCell.FlowDirection = Vector3Int.zero;
                working[source] = sourceCell;
                changed.Add(source);
            }

            int transferable = Mathf.Min(
                snapshotCell.Amount,
                sourceCell.Amount);

            Vector3Int below = source + Vector3Int.down;

            int movedDown = Transfer(
                working,
                source,
                below,
                type,
                Mathf.Min(
                    transferable,
                    tuning.maximumDownwardTransfer),
                changed);

            transferable -= movedDown;

            if (transferable <= 0 ||
                CanAcceptFluid(working, below, type))
            {
                continue;
            }

            int directionOffset =
                PositiveModulo(
                    source.x + source.y + source.z + tickIndex,
                    HorizontalDirections.Length);

            List<HorizontalCandidate> candidates = new();

            for (int directionIndex = 0;
                 directionIndex < HorizontalDirections.Length;
                 directionIndex++)
            {
                Vector3Int direction =
                    HorizontalDirections[
                        (directionIndex + directionOffset) %
                        HorizontalDirections.Length];

                Vector3Int target = source + direction;

                if (!CanAcceptFluid(working, target, type))
                {
                    continue;
                }

                candidates.Add(
                    new HorizontalCandidate(
                        target,
                        FindDrainDistance(
                            working,
                            target,
                            type,
                            tuning.drainSearchDistance),
                        directionIndex));
            }

            candidates.Sort(CompareCandidates);

            foreach (HorizontalCandidate candidate in candidates)
            {
                if (transferable <= 0)
                {
                    break;
                }

                Vector3Int target = candidate.Position;

                int targetAmount = GetWorkingAmount(
                    working,
                    target,
                    type);

                int difference = transferable - targetAmount;

                bool drainsDownhill = candidate.DrainDistance >= 0;

                if (!drainsDownhill &&
                    difference <= tuning.minimumHorizontalDifference)
                {
                    continue;
                }

                int available = drainsDownhill
                    ? transferable
                    : transferable - 1;

                if (available <= 0)
                {
                    continue;
                }

                int equalizingTransfer = difference / 2;

                if (drainsDownhill)
                {
                    equalizingTransfer = Mathf.Max(
                        1,
                        equalizingTransfer);
                }

                int desiredTransfer = Mathf.Min(
                    equalizingTransfer,
                    tuning.maximumHorizontalTransfer,
                    available);

                int moved = Transfer(
                    working,
                    source,
                    target,
                    type,
                    desiredTransfer,
                    changed);

                transferable -= moved;
            }
        }

        tickIndex++;
        ApplyWorkingState(type, working, changed);
    }

    private readonly struct HorizontalCandidate
    {
        public readonly Vector3Int Position;
        public readonly int DrainDistance;
        public readonly int Priority;

        public HorizontalCandidate(
            Vector3Int position,
            int drainDistance,
            int priority)
        {
            Position = position;
            DrainDistance = drainDistance;
            Priority = priority;
        }
    }

    private static int CompareCandidates(
        HorizontalCandidate left,
        HorizontalCandidate right)
    {
        bool leftDrains = left.DrainDistance >= 0;
        bool rightDrains = right.DrainDistance >= 0;

        if (leftDrains != rightDrains)
        {
            return leftDrains ? -1 : 1;
        }

        if (leftDrains)
        {
            int distance = left.DrainDistance.CompareTo(
                right.DrainDistance);

            if (distance != 0)
            {
                return distance;
            }
        }

        return left.Priority.CompareTo(right.Priority);
    }

    private int FindDrainDistance(
        Dictionary<Vector3Int, FluidCell> state,
        Vector3Int start,
        VoxelType type,
        int maximumDistance)
    {
        int clampedMaximum = Mathf.Max(1, maximumDistance);
        Queue<(Vector3Int Position, int Distance)> frontier = new();
        HashSet<Vector3Int> visited = new();

        frontier.Enqueue((start, 0));
        visited.Add(start);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            Vector3Int below = current.Position + Vector3Int.down;

            if (!world.ContainsExistingChunkAt(below) ||
                CanAcceptFluid(state, below, type))
            {
                return current.Distance;
            }

            if (current.Distance >= clampedMaximum)
            {
                continue;
            }

            foreach (Vector3Int direction in HorizontalDirections)
            {
                Vector3Int next = current.Position + direction;

                if (visited.Contains(next) ||
                    !world.ContainsExistingChunkAt(next) ||
                    !CanTraverseHorizontally(state, next, type))
                {
                    continue;
                }

                visited.Add(next);
                frontier.Enqueue((next, current.Distance + 1));
            }
        }

        return -1;
    }

    private bool CanTraverseHorizontally(
        Dictionary<Vector3Int, FluidCell> state,
        Vector3Int position,
        VoxelType type)
    {
        if (state.TryGetValue(position, out FluidCell cell))
        {
            return cell.Type == type;
        }

        return world.GetVoxel(position).Type == VoxelType.Air;
    }

    private int Transfer(
        Dictionary<Vector3Int, FluidCell> working,
        Vector3Int source,
        Vector3Int target,
        VoxelType type,
        int requestedAmount,
        HashSet<Vector3Int> changed)
    {
        if (requestedAmount <= 0 ||
            !working.TryGetValue(source, out FluidCell sourceCell) ||
            sourceCell.Type != type)
        {
            return 0;
        }

        // Leaving existing chunks means leaving the authored level.
        if (!world.ContainsExistingChunkAt(target))
        {
            int drained = Mathf.Min(requestedAmount, sourceCell.Amount);

            sourceCell.Amount -= drained;
            sourceCell.FlowDirection = target - source;
            working[source] = sourceCell;
            changed.Add(source);

            return drained;
        }

        if (!CanAcceptFluid(working, target, type))
        {
            return 0;
        }

        int targetAmount = GetWorkingAmount(working, target, type);
        int moved = Mathf.Min(
            requestedAmount,
            sourceCell.Amount,
            MaximumAmount - targetAmount);

        if (moved <= 0)
        {
            return 0;
        }

        Vector3Int direction = target - source;

        sourceCell.Amount -= moved;
        sourceCell.FlowDirection = direction;
        working[source] = sourceCell;

        FluidCell targetCell = working.TryGetValue(target, out FluidCell existing)
            ? existing
            : new FluidCell(type, 0);

        targetCell.Type = type;
        targetCell.Amount += moved;
        targetCell.FlowDirection = direction;
        working[target] = targetCell;

        changed.Add(source);
        changed.Add(target);

        return moved;
    }

    private bool CanAcceptFluid(
        Dictionary<Vector3Int, FluidCell> working,
        Vector3Int position,
        VoxelType type)
    {
        if (working.TryGetValue(position, out FluidCell fluidCell))
        {
            return fluidCell.Type == type &&
                   fluidCell.Amount < MaximumAmount;
        }

        return world.GetVoxel(position).Type == VoxelType.Air;
    }

    private static int GetWorkingAmount(
        Dictionary<Vector3Int, FluidCell> working,
        Vector3Int position,
        VoxelType type)
    {
        return working.TryGetValue(position, out FluidCell cell) &&
               cell.Type == type
            ? cell.Amount
            : 0;
    }

    private void ApplyWorkingState(
        VoxelType type,
        Dictionary<Vector3Int, FluidCell> working,
        HashSet<Vector3Int> changed)
    {
        HashSet<Vector3Int> nextActive = new();
        Dictionary<Vector3Int, Voxel> voxelChanges = new();

        foreach (Vector3Int position in changed)
        {
            bool hasFluid =
                working.TryGetValue(position, out FluidCell cell) &&
                cell.Type == type &&
                cell.Amount > 0;

            if (hasFluid)
            {
                cells[position] = cell;

                if (world.GetVoxel(position).Type != type)
                {
                    voxelChanges[position] = new Voxel(type);
                }
            }
            else
            {
                cells.Remove(position);

                if (world.GetVoxel(position).Type == type)
                {
                    voxelChanges[position] = new Voxel(VoxelType.Air);
                }
            }

            AddPositionAndNeighbours(nextActive, position, type, working);
        }

        applyingSimulationChanges = true;

        try
        {
            if (voxelChanges.Count > 0)
            {
                world.SetVoxelStates(voxelChanges);
            }
        }
        finally
        {
            applyingSimulationChanges = false;
        }

        // Amount changes also alter partial-height fluid meshes even when the
        // underlying voxel type remains Water or Lava.
        world.RefreshVoxelVisuals(changed);

        HashSet<Vector3Int> active = GetActiveSet(type);
        active.Clear();
        active.UnionWith(nextActive);
    }

    private void AddPositionAndNeighbours(
        HashSet<Vector3Int> targetSet,
        Vector3Int position,
        VoxelType type,
        Dictionary<Vector3Int, FluidCell> state)
    {
        TryAddActive(targetSet, position, type, state);
        TryAddActive(targetSet, position + Vector3Int.up, type, state);
        TryAddActive(targetSet, position + Vector3Int.down, type, state);

        foreach (Vector3Int direction in HorizontalDirections)
        {
            TryAddActive(targetSet, position + direction, type, state);
        }
    }

    private static void TryAddActive(
        HashSet<Vector3Int> active,
        Vector3Int position,
        VoxelType type,
        Dictionary<Vector3Int, FluidCell> state)
    {
        if (state.TryGetValue(position, out FluidCell cell) &&
            cell.Type == type &&
            cell.Amount > 0)
        {
            active.Add(position);
        }
    }

    private void HandleVoxelChanged(
        Vector3Int position,
        Voxel previous,
        Voxel current)
    {
        if (applyingSimulationChanges)
        {
            return;
        }

        bool previousWasFluid = IsFluid(previous.Type);
        bool currentIsFluid = IsFluid(current.Type);

        if (previousWasFluid)
        {
            cells.Remove(position);
            ActivateNeighbours(position, previous.Type);
        }

        if (currentIsFluid)
        {
            cells[position] =
                new FluidCell(current.Type, MaximumAmount);

            ActivateNeighbours(position, current.Type);
            GetActiveSet(current.Type).Add(position);
        }

        if (previous.Type != current.Type)
        {
            ActivateNeighbours(position, VoxelType.Water);
            ActivateNeighbours(position, VoxelType.Lava);
        }
    }

    private void ActivateNeighbours(
        Vector3Int position,
        VoxelType type)
    {
        HashSet<Vector3Int> active = GetActiveSet(type);

        ActivateIfMatching(active, position, type);
        ActivateIfMatching(active, position + Vector3Int.up, type);
        ActivateIfMatching(active, position + Vector3Int.down, type);

        foreach (Vector3Int direction in HorizontalDirections)
        {
            ActivateIfMatching(active, position + direction, type);
        }
    }

    private void ActivateIfMatching(
        HashSet<Vector3Int> active,
        Vector3Int position,
        VoxelType type)
    {
        if (cells.TryGetValue(position, out FluidCell cell) &&
            cell.Type == type)
        {
            active.Add(position);
        }
    }

    private HashSet<Vector3Int> GetActiveSet(VoxelType type)
    {
        return type == VoxelType.Lava
            ? activeLava
            : activeWater;
    }

    private static bool IsFluid(VoxelType type)
    {
        return type == VoxelType.Water ||
               type == VoxelType.Lava;
    }

    private static int ComparePositions(
        Vector3Int left,
        Vector3Int right)
    {
        int y = left.y.CompareTo(right.y);
        if (y != 0) return y;

        int x = left.x.CompareTo(right.x);
        if (x != 0) return x;

        return left.z.CompareTo(right.z);
    }

    private static int PositiveModulo(int value, int divisor)
    {
        int result = value % divisor;
        return result < 0 ? result + divisor : result;
    }
}
