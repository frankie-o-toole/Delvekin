using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns Water runtime state independently from the legacy Lava solver.
///
/// Authored water is static. Connected WaterBody topology is cached and finite
/// redistribution is requested only when terrain is removed beside water.
/// </summary>
public sealed class WaterSystem : IDisposable
{
    public const int MaximumAmount = (int)WaterAmount.Full;

    private const int MaximumRedistributionCells = 65536;

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
    private readonly HashSet<Vector3Int> pendingOpenings = new();

    private bool topologyDirty;
    private bool applyingRedistribution;
    private int nextBodyId = 1;

    public bool HasPendingWork =>
        topologyDirty || pendingOpenings.Count > 0;

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
        pendingOpenings.Clear();
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
    /// Collapses all edits made since the previous frame into one topology
    /// pass and at most one redistribution per touched finite body.
    /// </summary>
    public void ProcessPendingWork()
    {
        if (!HasPendingWork)
        {
            return;
        }

        if (topologyDirty)
        {
            RebuildBodies();
            topologyDirty = false;
        }

        if (pendingOpenings.Count == 0)
        {
            return;
        }

        HashSet<int> affectedBodyIds = new();

        foreach (Vector3Int opening in pendingOpenings)
        {
            foreach (Vector3Int direction in CardinalDirections)
            {
                Vector3Int neighbour = opening + direction;

                if (bodyByPosition.TryGetValue(
                        neighbour,
                        out int bodyId))
                {
                    affectedBodyIds.Add(bodyId);
                }
            }
        }

        List<Vector3Int> openings =
            new(pendingOpenings);

        pendingOpenings.Clear();

        bool redistributed = false;

        foreach (int bodyId in affectedBodyIds)
        {
            if (!bodies.TryGetValue(
                    bodyId,
                    out WaterBody body) ||
                body.Kind != WaterBodyKind.Finite)
            {
                continue;
            }

            if (RedistributeFiniteBody(body, openings))
            {
                redistributed = true;
            }
        }

        if (redistributed)
        {
            RebuildBodies();
        }

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

    private bool RedistributeFiniteBody(
        WaterBody body,
        IReadOnlyCollection<Vector3Int> allOpenings)
    {
        HashSet<Vector3Int> bodyCells =
            new(body.Cells);

        List<Vector3Int> relevantOpenings = new();

        foreach (Vector3Int opening in allOpenings)
        {
            foreach (Vector3Int direction in CardinalDirections)
            {
                if (bodyCells.Contains(opening + direction))
                {
                    relevantOpenings.Add(opening);
                    break;
                }
            }
        }

        if (relevantOpenings.Count == 0)
        {
            return false;
        }

        HashSet<Vector3Int> reachableAir = new();
        Dictionary<Vector3Int, int> airDistance = new();
        Queue<Vector3Int> frontier = new();

        foreach (Vector3Int opening in relevantOpenings)
        {
            if (!CanSearchAir(opening, body.HighestCellY) ||
                !reachableAir.Add(opening))
            {
                continue;
            }

            airDistance[opening] = 0;
            frontier.Enqueue(opening);
        }

        bool foundLowerSpace = false;
        bool searchLimitReached = false;

        while (frontier.Count > 0)
        {
            Vector3Int position = frontier.Dequeue();
            int distance = airDistance[position];

            if (position.y < body.HighestCellY)
            {
                foundLowerSpace = true;
            }

            if (reachableAir.Count >= MaximumRedistributionCells)
            {
                searchLimitReached = true;
                break;
            }

            foreach (Vector3Int direction in CardinalDirections)
            {
                Vector3Int neighbour = position + direction;

                if (!CanSearchAir(neighbour, body.HighestCellY) ||
                    !reachableAir.Add(neighbour))
                {
                    continue;
                }

                airDistance[neighbour] = distance + 1;
                frontier.Enqueue(neighbour);
            }
        }

        if (searchLimitReached)
        {
            Debug.LogWarning(
                $"Water body {body.Id} redistribution aborted: " +
                $"reachable space exceeded {MaximumRedistributionCells} cells.");

            return false;
        }

        // Same-height openings do not cause a finite body to spread. There
        // must be newly reachable space below its original surface.
        if (!foundLowerSpace)
        {
            return false;
        }

        List<RedistributionCandidate> candidates =
            new(bodyCells.Count + reachableAir.Count);

        foreach (Vector3Int position in bodyCells)
        {
            candidates.Add(
                new RedistributionCandidate(
                    position,
                    true,
                    DistanceToNearestOpening(
                        position,
                        relevantOpenings)));
        }

        foreach (Vector3Int position in reachableAir)
        {
            candidates.Add(
                new RedistributionCandidate(
                    position,
                    false,
                    airDistance[position]));
        }

        candidates.Sort(CompareCandidates);

        int remainingUnits = body.AmountUnits;
        Dictionary<Vector3Int, int> desiredAmounts = new();

        foreach (RedistributionCandidate candidate in candidates)
        {
            if (remainingUnits <= 0)
            {
                break;
            }

            int amount = Mathf.Min(MaximumAmount, remainingUnits);
            desiredAmounts[candidate.Position] = amount;
            remainingUnits -= amount;
        }

        if (remainingUnits > 0)
        {
            Debug.LogWarning(
                $"Water body {body.Id} redistribution aborted: " +
                $"{remainingUnits} half-units had no destination.");

            return false;
        }

        return ApplyRedistribution(bodyCells, desiredAmounts);
    }

    private bool ApplyRedistribution(
        HashSet<Vector3Int> previousPositions,
        IReadOnlyDictionary<Vector3Int, int> desiredAmounts)
    {
        Dictionary<Vector3Int, Voxel> voxelChanges = new();
        HashSet<Vector3Int> visualChanges = new();
        bool changed = false;

        applyingRedistribution = true;

        try
        {
            foreach (Vector3Int position in previousPositions)
            {
                if (desiredAmounts.ContainsKey(position))
                {
                    continue;
                }

                cells.Remove(position);
                voxelChanges[position] =
                    new Voxel(VoxelType.Air);
                visualChanges.Add(position);
                changed = true;
            }

            foreach (var pair in desiredAmounts)
            {
                Vector3Int position = pair.Key;
                int desiredAmount = pair.Value;
                int previousAmount = GetAmount(position);

                if (previousAmount == desiredAmount)
                {
                    continue;
                }

                if (previousAmount == 0)
                {
                    cells[position] =
                        new WaterCell((WaterAmount)desiredAmount);

                    voxelChanges[position] =
                        new Voxel(VoxelType.Water);
                }
                else
                {
                    WaterCell cell = cells[position];
                    cell.Amount = (WaterAmount)desiredAmount;
                    cells[position] = cell;
                }

                visualChanges.Add(position);
                changed = true;
            }

            if (voxelChanges.Count > 0)
            {
                world.SetFluidVoxelStates(voxelChanges);
            }
        }
        finally
        {
            applyingRedistribution = false;
        }

        if (visualChanges.Count > 0)
        {
            world.RefreshVoxelVisuals(visualChanges);
        }

        return changed;
    }

    private bool CanSearchAir(
        Vector3Int position,
        int maximumY)
    {
        return
            position.y <= maximumY &&
            world.ContainsExistingChunkAt(position) &&
            world.GetVoxel(position).Type == VoxelType.Air;
    }

    private static int DistanceToNearestOpening(
        Vector3Int position,
        IReadOnlyList<Vector3Int> openings)
    {
        int nearest = int.MaxValue;

        foreach (Vector3Int opening in openings)
        {
            int distance =
                Mathf.Abs(position.x - opening.x) +
                Mathf.Abs(position.y - opening.y) +
                Mathf.Abs(position.z - opening.z);

            nearest = Mathf.Min(nearest, distance);
        }

        return nearest;
    }

    private static int CompareCandidates(
        RedistributionCandidate left,
        RedistributionCandidate right)
    {
        int comparison =
            left.Position.y.CompareTo(right.Position.y);

        if (comparison != 0)
        {
            return comparison;
        }

        // At equal height, preserve authored water before occupying new air.
        comparison =
            right.WasWater.CompareTo(left.WasWater);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Distance.CompareTo(right.Distance);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Position.x.CompareTo(right.Position.x);

        if (comparison != 0)
        {
            return comparison;
        }

        return left.Position.z.CompareTo(right.Position.z);
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

                    if (unassigned.Remove(neighbour))
                    {
                        frontier.Enqueue(neighbour);
                    }
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
        if (applyingRedistribution)
        {
            return;
        }

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

        bool openedTerrain =
            previous.Type != VoxelType.Air &&
            previous.Type != VoxelType.Water &&
            current.Type == VoxelType.Air &&
            TouchesWater(position);

        if (openedTerrain)
        {
            pendingOpenings.Add(position);
        }

        if (waterOccupancyChanged || openedTerrain)
        {
            topologyDirty = true;
        }
    }

    private readonly struct RedistributionCandidate
    {
        public Vector3Int Position { get; }
        public bool WasWater { get; }
        public int Distance { get; }

        public RedistributionCandidate(
            Vector3Int position,
            bool wasWater,
            int distance)
        {
            Position = position;
            WasWater = wasWater;
            Distance = distance;
        }
    }
}
