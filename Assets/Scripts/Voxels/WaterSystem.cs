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
    private const float RedistributionTickInterval = 0.15f;
    private const int FlowDepthPerTick = 3;
    private const int MaximumTransferredUnitsPerTick = 128;
    private const int MaximumCatchUpTicksPerFrame = 2;

    private static readonly Vector3Int[] CardinalDirections =
    {
        Vector3Int.forward,
        Vector3Int.right,
        Vector3Int.back,
        Vector3Int.left,
        Vector3Int.up,
        Vector3Int.down
    };

    // Current spreads horizontally through rivers and may fall. Upward
    // traversal keeps connected multi-level bodies discoverable, but is never
    // selected as an outgoing gameplay current.
    private static readonly Vector3Int[] FlowTraversalDirections =
    {
        Vector3Int.forward,
        Vector3Int.right,
        Vector3Int.back,
        Vector3Int.left,
        Vector3Int.down,
        Vector3Int.up
    };

    private readonly VoxelWorld world;
    private readonly Dictionary<Vector3Int, WaterCell> cells = new();
    private readonly Dictionary<Vector3Int, WaterSource> sources = new();
    private readonly HashSet<WaterSourcePortal> sourcePortals = new();
    private readonly HashSet<WaterOutletPortal> outletPortals = new();
    private readonly List<Vector3Int> portalVoxelBuffer = new();
    private readonly Dictionary<int, WaterBody> bodies = new();
    private readonly Dictionary<Vector3Int, int> bodyByPosition = new();
    private readonly HashSet<Vector3Int> pendingOpenings = new();
    private readonly Queue<WaterRedistributionPlan> activePlans = new();

    private bool topologyDirty;
    private bool applyingRedistribution;
    private int nextBodyId = 1;
    private float redistributionTickTimer;

    public bool HasPendingWork =>
        topologyDirty ||
        pendingOpenings.Count > 0 ||
        activePlans.Count > 0;

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
        sources.Clear();
        bodies.Clear();
        bodyByPosition.Clear();
        pendingOpenings.Clear();
        activePlans.Clear();
        redistributionTickTimer = 0f;
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
    public void ProcessPendingWork(float deltaTime)
    {
        if (!HasPendingWork)
        {
            redistributionTickTimer = 0f;
            return;
        }

        if (activePlans.Count > 0)
        {
            redistributionTickTimer += deltaTime;
            int ticks = 0;

            while (redistributionTickTimer >=
                       RedistributionTickInterval &&
                   ticks < MaximumCatchUpTicksPerFrame &&
                   activePlans.Count > 0)
            {
                redistributionTickTimer -=
                    RedistributionTickInterval;

                ApplyNextRedistributionTick();
                ticks++;
            }

            return;
        }

        redistributionTickTimer = 0f;

        if (topologyDirty)
        {
            RebuildBodies();
            topologyDirty = false;
            QueueExistingSourceFalls();
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

        WaterBody sourceBody = null;

        foreach (int bodyId in affectedBodyIds)
        {
            if (!bodies.TryGetValue(bodyId, out WaterBody body) ||
                body.Kind != WaterBodyKind.SourceFed)
            {
                continue;
            }

            if (sourceBody == null ||
                body.MaximumSourceLevelY >
                sourceBody.MaximumSourceLevelY)
            {
                sourceBody = body;
            }
        }

        if (sourceBody != null)
        {
            BuildSourceFedRedistributionPlan(sourceBody, openings);
        }
        else
        {
            foreach (int bodyId in affectedBodyIds)
            {
                if (bodies.TryGetValue(
                        bodyId,
                        out WaterBody body) &&
                    body.Kind == WaterBodyKind.Finite)
                {
                    BuildFiniteRedistributionPlan(body, openings);
                }
            }
        }

        topologyDirty = false;
    }

    public void RegisterSourcePortal(WaterSourcePortal portal)
    {
        if (portal != null && sourcePortals.Add(portal))
        {
            topologyDirty = true;
        }
    }

    public void UnregisterSourcePortal(WaterSourcePortal portal)
    {
        if (portal != null && sourcePortals.Remove(portal))
        {
            topologyDirty = true;
        }
    }

    public void NotifySourcePortalChanged(WaterSourcePortal portal)
    {
        if (portal != null && sourcePortals.Contains(portal))
        {
            topologyDirty = true;
        }
    }

    public void RegisterOutletPortal(WaterOutletPortal portal)
    {
        if (portal != null && outletPortals.Add(portal))
        {
            topologyDirty = true;
        }
    }

    public void UnregisterOutletPortal(WaterOutletPortal portal)
    {
        if (portal != null && outletPortals.Remove(portal))
        {
            topologyDirty = true;
        }
    }

    public void NotifyOutletPortalChanged(WaterOutletPortal portal)
    {
        if (portal != null && outletPortals.Contains(portal))
        {
            topologyDirty = true;
        }
    }

    public bool IsSource(Vector3Int position)
    {
        return sources.ContainsKey(position);
    }

    public bool ToggleSource(Vector3Int position)
    {
        if (!cells.ContainsKey(position) ||
            world.GetVoxel(position).Type != VoxelType.Water)
        {
            return false;
        }

        if (sources.Remove(position))
        {
            topologyDirty = true;
            Debug.Log($"Removed Water source at {position}.");
            return true;
        }

        int maximumLevelY = position.y;

        if (TryGetBody(position, out WaterBody body))
        {
            maximumLevelY = body.HighestCellY;
        }

        sources[position] = new WaterSource(
            position,
            maximumLevelY,
            MaximumTransferredUnitsPerTick,
            Vector3Int.forward);

        topologyDirty = true;

        Debug.Log(
            $"Added Water source at {position}, " +
            $"maximum level Y={maximumLevelY}.");

        return true;
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

    public Vector3Int GetSecondaryFlowDirection(Vector3Int position)
    {
        return cells.TryGetValue(position, out WaterCell cell)
            ? cell.SecondaryFlowDirection
            : Vector3Int.zero;
    }

    public Vector3 GetFlowVector(Vector3Int position)
    {
        if (!cells.TryGetValue(position, out WaterCell cell) ||
            cell.Motion != WaterMotion.Flowing)
        {
            return Vector3.zero;
        }

        Vector3 weighted =
            (Vector3)cell.PrimaryFlowDirection * cell.PrimaryCapacity +
            (Vector3)cell.SecondaryFlowDirection * cell.SecondaryCapacity;

        return weighted.sqrMagnitude > 0f
            ? weighted.normalized
            : Vector3.zero;
    }

    private void QueueExistingSourceFalls()
    {
        foreach (WaterBody body in bodies.Values)
        {
            if (body.Kind != WaterBodyKind.SourceFed)
            {
                continue;
            }

            // A source-to-outlet river is authored as a complete voxel route.
            // Do not reinterpret every exposed shore or ledge as a new breach
            // on startup; the outlet is the deliberate hand-off boundary.
            // Terrain removed later still enters pendingOpenings through
            // HandleVoxelChanged and is redistributed normally.
            if (body.OutletCount > 0)
            {
                continue;
            }

            HashSet<Vector3Int> outletCells =
                GetOutletCells(body);

            foreach (Vector3Int position in body.Cells)
            {
                // An outlet hands water to a waterfall/VFX volume. The voxel
                // solver deliberately stops here instead of searching the
                // open world beyond the portal.
                if (outletCells.Contains(position))
                {
                    continue;
                }

                Vector3Int below = position + Vector3Int.down;

                if (world.ContainsExistingChunkAt(below) &&
                    world.GetVoxel(below).Type == VoxelType.Air)
                {
                    pendingOpenings.Add(below);
                }

                foreach (Vector3Int direction in CardinalDirections)
                {
                    if (direction.y != 0)
                    {
                        continue;
                    }

                    Vector3Int edge = position + direction;
                    Vector3Int belowEdge = edge + Vector3Int.down;

                    if (!world.ContainsExistingChunkAt(edge) ||
                        !world.ContainsExistingChunkAt(belowEdge) ||
                        world.GetVoxel(edge).Type != VoxelType.Air)
                    {
                        continue;
                    }

                    VoxelType supportType =
                        world.GetVoxel(belowEdge).Type;

                    if (supportType == VoxelType.Air ||
                        supportType == VoxelType.Water)
                    {
                        pendingOpenings.Add(edge);
                    }
                }
            }
        }
    }


    private HashSet<Vector3Int> GetOutletCells(WaterBody body)
    {
        HashSet<Vector3Int> result = new();

        foreach (WaterOutletPortal outlet in body.Outlets)
        {
            if (outlet == null)
            {
                continue;
            }

            portalVoxelBuffer.Clear();
            outlet.GetCoveredVoxels(portalVoxelBuffer);

            foreach (Vector3Int position in portalVoxelBuffer)
            {
                if (bodyByPosition.TryGetValue(position, out int bodyId) &&
                    bodyId == body.Id)
                {
                    result.Add(position);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Builds a cached gameplay-current network. This runs only after water
    /// topology or a portal changes; stable rivers have no per-frame cost.
    ///
    /// Distance grows away from every source. Each cell points to downstream
    /// neighbours. Downstream capacity makes the widest/longest continuation
    /// primary while preserving a second valid branch.
    /// </summary>
    private void RebuildFlowFields()
    {
        List<Vector3Int> allPositions = new(cells.Keys);

        foreach (Vector3Int position in allPositions)
        {
            WaterCell cell = cells[position];
            cell.Motion = WaterMotion.Still;
            cell.PrimaryFlowDirection = Vector3Int.zero;
            cell.SecondaryFlowDirection = Vector3Int.zero;
            cell.PrimaryCapacity = 0;
            cell.SecondaryCapacity = 0;
            cells[position] = cell;
        }

        foreach (WaterBody body in bodies.Values)
        {
            if (body.Kind == WaterBodyKind.SourceFed)
            {
                BuildFlowField(body);
            }
        }
    }

    private void BuildFlowField(WaterBody body)
    {
        HashSet<Vector3Int> bodyCells = new(body.Cells);
        Dictionary<Vector3Int, Vector3Int> preferredDirections = new();
        HashSet<Vector3Int> seeds = new();

        foreach (WaterSource source in body.Sources)
        {
            if (bodyCells.Contains(source.Position))
            {
                seeds.Add(source.Position);
                preferredDirections[source.Position] =
                    source.InitialDirection;
            }
        }

        // A portal is a volume, not a single point. Seeding every overlapped
        // water voxel gives a broad river a broad, parallel current instead
        // of a radial fan from the portal's minimum corner.
        foreach (WaterSourcePortal portal in sourcePortals)
        {
            if (portal == null)
            {
                continue;
            }

            portalVoxelBuffer.Clear();
            portal.GetCoveredVoxels(portalVoxelBuffer);

            foreach (Vector3Int position in portalVoxelBuffer)
            {
                if (!bodyCells.Contains(position))
                {
                    continue;
                }

                seeds.Add(position);
                preferredDirections[position] = portal.Direction;
            }
        }

        if (seeds.Count == 0)
        {
            return;
        }

        Dictionary<Vector3Int, int> distance = new();
        Dictionary<Vector3Int, Vector3Int> predecessor = new();
        Queue<Vector3Int> frontier = new();

        foreach (Vector3Int seed in seeds)
        {
            distance[seed] = 0;
            frontier.Enqueue(seed);
        }

        while (frontier.Count > 0)
        {
            Vector3Int position = frontier.Dequeue();
            int nextDistance = distance[position] + 1;

            foreach (Vector3Int direction in FlowTraversalDirections)
            {
                Vector3Int neighbour = position + direction;

                if (!bodyCells.Contains(neighbour) ||
                    distance.ContainsKey(neighbour))
                {
                    continue;
                }

                distance[neighbour] = nextDistance;
                predecessor[neighbour] = position;
                frontier.Enqueue(neighbour);
            }
        }

        List<Vector3Int> ordered = new(distance.Keys);
        ordered.Sort(
            (left, right) =>
                distance[right].CompareTo(distance[left]));

        Dictionary<Vector3Int, int> downstreamCapacity = new();

        foreach (Vector3Int position in ordered)
        {
            List<Vector3Int> outgoing =
                GetOutgoingFlowNeighbours(position, bodyCells, distance);

            int capacity = 1;

            foreach (Vector3Int neighbour in outgoing)
            {
                if (downstreamCapacity.TryGetValue(
                        neighbour,
                        out int branchCapacity))
                {
                    capacity = Mathf.Min(
                        byte.MaxValue,
                        capacity + branchCapacity);
                }
            }

            downstreamCapacity[position] = capacity;
        }

        Dictionary<Vector3Int, OutletFlow> outletFlows =
            BuildOutletFlowMap(body);

        foreach (Vector3Int position in distance.Keys)
        {
            if (outletFlows.TryGetValue(
                    position,
                    out OutletFlow outletFlow))
            {
                SetFlow(
                    position,
                    outletFlow.Direction,
                    outletFlow.Capacity,
                    Vector3Int.zero,
                    0);
                continue;
            }

            List<Vector3Int> outgoing =
                GetOutgoingFlowNeighbours(position, bodyCells, distance);

            if (outgoing.Count == 0)
            {
                continue;
            }

            Vector3Int preferred = Vector3Int.zero;

            if (!preferredDirections.TryGetValue(position, out preferred) &&
                predecessor.TryGetValue(position, out Vector3Int previous))
            {
                preferred = position - previous;
            }

            outgoing.Sort(
                (left, right) =>
                {
                    // River inertia is more readable than turning toward
                    // the globally largest downstream region. Continue in
                    // the incoming direction while that route exists; branch
                    // capacity only decides between equally plausible turns.
                    bool leftPreferred =
                        left - position == preferred;
                    bool rightPreferred =
                        right - position == preferred;

                    if (leftPreferred != rightPreferred)
                    {
                        return rightPreferred.CompareTo(leftPreferred);
                    }

                    int leftCapacity = downstreamCapacity[left];
                    int rightCapacity = downstreamCapacity[right];
                    int comparison =
                        rightCapacity.CompareTo(leftCapacity);

                    if (comparison != 0)
                    {
                        return comparison;
                    }

                    comparison = left.x.CompareTo(right.x);

                    return comparison != 0
                        ? comparison
                        : left.z.CompareTo(right.z);
                });

            Vector3Int primaryNeighbour = outgoing[0];
            Vector3Int primaryDirection =
                primaryNeighbour - position;
            int primaryCapacity =
                downstreamCapacity[primaryNeighbour];

            Vector3Int secondaryDirection = Vector3Int.zero;
            int secondaryCapacity = 0;

            if (outgoing.Count > 1)
            {
                Vector3Int secondaryNeighbour = outgoing[1];
                secondaryDirection =
                    secondaryNeighbour - position;
                secondaryCapacity =
                    downstreamCapacity[secondaryNeighbour];
            }

            SetFlow(
                position,
                primaryDirection,
                primaryCapacity,
                secondaryDirection,
                secondaryCapacity);
        }
    }

    private static List<Vector3Int> GetOutgoingFlowNeighbours(
        Vector3Int position,
        HashSet<Vector3Int> bodyCells,
        IReadOnlyDictionary<Vector3Int, int> distance)
    {
        List<Vector3Int> horizontal = new(4);
        int currentDistance = distance[position];

        for (int index = 0; index < 4; index++)
        {
            Vector3Int neighbour =
                position + FlowTraversalDirections[index];

            if (bodyCells.Contains(neighbour) &&
                distance.TryGetValue(neighbour, out int neighbourDistance) &&
                neighbourDistance == currentDistance + 1)
            {
                horizontal.Add(neighbour);
            }
        }

        if (horizontal.Count > 0)
        {
            return horizontal;
        }

        Vector3Int below = position + Vector3Int.down;

        if (bodyCells.Contains(below) &&
            distance.TryGetValue(below, out int belowDistance) &&
            belowDistance == currentDistance + 1)
        {
            horizontal.Add(below);
        }

        return horizontal;
    }

    private Dictionary<Vector3Int, OutletFlow> BuildOutletFlowMap(
        WaterBody body)
    {
        Dictionary<Vector3Int, OutletFlow> result = new();

        foreach (WaterOutletPortal outlet in body.Outlets)
        {
            if (outlet == null)
            {
                continue;
            }

            portalVoxelBuffer.Clear();
            outlet.GetCoveredVoxels(portalVoxelBuffer);

            foreach (Vector3Int position in portalVoxelBuffer)
            {
                if (bodyByPosition.TryGetValue(position, out int bodyId) &&
                    bodyId == body.Id)
                {
                    result[position] =
                        new OutletFlow(
                            outlet.Direction,
                            outlet.Capacity);
                }
            }
        }

        return result;
    }

    private void SetFlow(
        Vector3Int position,
        Vector3Int primaryDirection,
        int primaryCapacity,
        Vector3Int secondaryDirection,
        int secondaryCapacity)
    {
        if (!cells.TryGetValue(position, out WaterCell cell) ||
            primaryDirection == Vector3Int.zero)
        {
            return;
        }

        cell.Motion = WaterMotion.Flowing;
        cell.PrimaryFlowDirection = primaryDirection;
        cell.SecondaryFlowDirection = secondaryDirection;
        cell.PrimaryCapacity =
            (byte)Mathf.Clamp(primaryCapacity, 1, byte.MaxValue);
        cell.SecondaryCapacity =
            secondaryDirection == Vector3Int.zero
                ? (byte)0
                : (byte)Mathf.Clamp(
                    secondaryCapacity,
                    1,
                    byte.MaxValue);
        cells[position] = cell;
    }

    private bool BuildSourceFedRedistributionPlan(
        WaterBody sourceBody,
        IReadOnlyCollection<Vector3Int> allOpenings)
    {
        HashSet<Vector3Int> sourceCells =
            new(sourceBody.Cells);
        List<Vector3Int> relevantOpenings = new();

        foreach (Vector3Int opening in allOpenings)
        {
            foreach (Vector3Int direction in CardinalDirections)
            {
                if (sourceCells.Contains(opening + direction))
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

        HashSet<Vector3Int> visited = new();
        Dictionary<Vector3Int, int> distance = new();
        Queue<Vector3Int> frontier = new();

        foreach (Vector3Int opening in relevantOpenings)
        {
            if (!CanSearchSourceSpace(
                    opening,
                    sourceBody.MaximumSourceLevelY,
                    sourceCells) ||
                !visited.Add(opening))
            {
                continue;
            }

            distance[opening] = 0;
            frontier.Enqueue(opening);
        }

        List<PlanUnit> additions = new();
        bool searchLimitReached = false;

        while (frontier.Count > 0)
        {
            Vector3Int position = frontier.Dequeue();
            int currentDistance = distance[position];
            VoxelType type = world.GetVoxel(position).Type;

            if (type == VoxelType.Air)
            {
                additions.Add(
                    new PlanUnit(position, currentDistance));
                additions.Add(
                    new PlanUnit(position, currentDistance));
            }

            if (visited.Count >= MaximumRedistributionCells)
            {
                searchLimitReached = true;
                break;
            }

            foreach (Vector3Int direction in CardinalDirections)
            {
                Vector3Int neighbour = position + direction;

                if (!CanSearchSourceSpace(
                        neighbour,
                        sourceBody.MaximumSourceLevelY,
                        sourceCells) ||
                    !visited.Add(neighbour))
                {
                    continue;
                }

                distance[neighbour] = currentDistance + 1;
                frontier.Enqueue(neighbour);
            }
        }

        if (searchLimitReached)
        {
            Debug.LogWarning(
                $"Source-fed water body {sourceBody.Id} fill aborted: " +
                $"reachable space exceeded {MaximumRedistributionCells} cells.");

            return false;
        }

        if (additions.Count == 0)
        {
            return false;
        }

        additions.Sort(ComparePlanAdditions);

        activePlans.Enqueue(
            new WaterRedistributionPlan(
                additions,
                Array.Empty<PlanUnit>(),
                generatesWater: true,
                unitsPerTick: sourceBody.TotalSourceUnitsPerTick));

        return true;
    }

    private bool BuildFiniteRedistributionPlan(
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

        WaterRedistributionPlan plan =
            CreateRedistributionPlan(
                bodyCells,
                desiredAmounts,
                relevantOpenings,
                airDistance);

        if (plan == null)
        {
            return false;
        }

        activePlans.Enqueue(plan);
        return true;
    }

    private WaterRedistributionPlan CreateRedistributionPlan(
        IReadOnlyCollection<Vector3Int> previousPositions,
        IReadOnlyDictionary<Vector3Int, int> desiredAmounts,
        IReadOnlyList<Vector3Int> openings,
        IReadOnlyDictionary<Vector3Int, int> airDistance)
    {
        List<PlanUnit> additions = new();
        List<PlanUnit> removals = new();
        HashSet<Vector3Int> allPositions =
            new(previousPositions);

        foreach (Vector3Int position in desiredAmounts.Keys)
        {
            allPositions.Add(position);
        }

        foreach (Vector3Int position in allPositions)
        {
            int previousAmount = GetAmount(position);
            int desiredAmount =
                desiredAmounts.TryGetValue(position, out int value)
                    ? value
                    : 0;

            int distance =
                airDistance.TryGetValue(position, out int airSteps)
                    ? airSteps
                    : DistanceToNearestOpening(position, openings);

            for (int unit = previousAmount;
                 unit < desiredAmount;
                 unit++)
            {
                additions.Add(new PlanUnit(position, distance));
            }

            for (int unit = desiredAmount;
                 unit < previousAmount;
                 unit++)
            {
                removals.Add(new PlanUnit(position, distance));
            }
        }

        if (additions.Count == 0)
        {
            return null;
        }

        if (additions.Count != removals.Count)
        {
            Debug.LogError(
                "Finite water plan rejected because its added and " +
                "removed half-units do not match.");

            return null;
        }

        additions.Sort(ComparePlanAdditions);
        removals.Sort(ComparePlanRemovals);

        return new WaterRedistributionPlan(
            additions,
            removals,
            generatesWater: false,
            unitsPerTick: MaximumTransferredUnitsPerTick);
    }

    private void ApplyNextRedistributionTick()
    {
        if (activePlans.Count == 0)
        {
            return;
        }

        WaterRedistributionPlan plan = activePlans.Peek();
        plan.AdvanceFlowFront(FlowDepthPerTick);

        Dictionary<Vector3Int, int> amountDeltas = new();
        int transferredUnits = 0;
        int tickBudget = Mathf.Min(
            MaximumTransferredUnitsPerTick,
            plan.UnitsPerTick);

        while (transferredUnits < tickBudget &&
               plan.TryTakeTransfer(
                   out Vector3Int source,
                   out Vector3Int destination,
                   out bool generated))
        {
            if (!generated)
            {
                AddDelta(amountDeltas, source, -1);
            }

            AddDelta(amountDeltas, destination, +1);
            transferredUnits++;
        }

        if (amountDeltas.Count > 0)
        {
            ApplyAmountDeltas(amountDeltas);
        }

        if (!plan.IsComplete)
        {
            return;
        }

        activePlans.Dequeue();
        RebuildBodies();
        topologyDirty = false;
    }

    private void ApplyAmountDeltas(
        IReadOnlyDictionary<Vector3Int, int> amountDeltas)
    {
        Dictionary<Vector3Int, Voxel> voxelChanges = new();
        HashSet<Vector3Int> visualChanges = new();

        applyingRedistribution = true;

        try
        {
            foreach (var pair in amountDeltas)
            {
                Vector3Int position = pair.Key;
                int previousAmount = GetAmount(position);
                int resultingAmount = previousAmount + pair.Value;

                if (resultingAmount < 0 ||
                    resultingAmount > MaximumAmount)
                {
                    Debug.LogError(
                        $"Invalid water amount {resultingAmount} at " +
                        $"{position} during redistribution.");

                    continue;
                }

                if (resultingAmount == 0)
                {
                    cells.Remove(position);
                    voxelChanges[position] =
                        new Voxel(VoxelType.Air);
                }
                else if (previousAmount == 0)
                {
                    cells[position] =
                        new WaterCell((WaterAmount)resultingAmount);

                    voxelChanges[position] =
                        new Voxel(VoxelType.Water);
                }
                else
                {
                    WaterCell cell = cells[position];
                    cell.Amount = (WaterAmount)resultingAmount;
                    cells[position] = cell;
                }

                visualChanges.Add(position);
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
    }

    private static void AddDelta(
        IDictionary<Vector3Int, int> deltas,
        Vector3Int position,
        int amount)
    {
        deltas.TryGetValue(position, out int current);
        deltas[position] = current + amount;
    }

    private static int ComparePlanAdditions(
        PlanUnit left,
        PlanUnit right)
    {
        int comparison = left.Distance.CompareTo(right.Distance);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Position.y.CompareTo(right.Position.y);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Position.x.CompareTo(right.Position.x);

        return comparison != 0
            ? comparison
            : left.Position.z.CompareTo(right.Position.z);
    }

    private static int ComparePlanRemovals(
        PlanUnit left,
        PlanUnit right)
    {
        int comparison = right.Position.y.CompareTo(left.Position.y);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = right.Distance.CompareTo(left.Distance);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Position.x.CompareTo(right.Position.x);

        return comparison != 0
            ? comparison
            : left.Position.z.CompareTo(right.Position.z);
    }

    private bool CanSearchSourceSpace(
        Vector3Int position,
        int maximumY,
        HashSet<Vector3Int> originalSourceCells)
    {
        if (position.y > maximumY ||
            !world.ContainsExistingChunkAt(position))
        {
            return false;
        }

        VoxelType type = world.GetVoxel(position).Type;

        if (type == VoxelType.Air)
        {
            return true;
        }

        // Water belonging to another body may be crossed so a connected
        // finite lake inherits the source. Do not traverse back through the
        // original source body and escape through unrelated authored shores.
        return
            type == VoxelType.Water &&
            !originalSourceCells.Contains(position);
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

        foreach (WaterSource source in sources.Values)
        {
            if (bodyByPosition.TryGetValue(
                    source.Position,
                    out int bodyId) &&
                bodies.TryGetValue(bodyId, out WaterBody body))
            {
                body.AddSource(source);
            }
        }

        foreach (WaterSourcePortal portal in sourcePortals)
        {
            if (portal == null)
            {
                continue;
            }

            portalVoxelBuffer.Clear();
            portal.GetCoveredVoxels(portalVoxelBuffer);
            HashSet<int> touchedBodies = new();

            foreach (Vector3Int position in portalVoxelBuffer)
            {
                if (bodyByPosition.TryGetValue(position, out int bodyId))
                {
                    touchedBodies.Add(bodyId);
                }
            }

            if (touchedBodies.Count == 0)
            {
                Debug.LogWarning(
                    $"Water Source Portal '{portal.name}' does not overlap " +
                    "any Water voxel.");
            }

            foreach (int bodyId in touchedBodies)
            {
                if (!bodies.TryGetValue(bodyId, out WaterBody body))
                {
                    continue;
                }

                body.AddSource(
                    new WaterSource(
                        portal.MinimumVoxel,
                        portal.MaximumLevelY,
                        portal.SupplyUnitsPerTick,
                        portal.Direction));
            }
        }

        foreach (WaterOutletPortal portal in outletPortals)
        {
            if (portal == null)
            {
                continue;
            }

            portalVoxelBuffer.Clear();
            portal.GetCoveredVoxels(portalVoxelBuffer);
            HashSet<int> touchedBodies = new();

            foreach (Vector3Int position in portalVoxelBuffer)
            {
                if (bodyByPosition.TryGetValue(position, out int bodyId))
                {
                    touchedBodies.Add(bodyId);
                }
            }

            if (touchedBodies.Count == 0)
            {
                Debug.LogWarning(
                    $"Water Outlet Portal '{portal.name}' does not overlap " +
                    "any Water voxel.");
            }

            foreach (int bodyId in touchedBodies)
            {
                if (bodies.TryGetValue(bodyId, out WaterBody body))
                {
                    body.AddOutlet(portal);
                }
            }
        }

        RebuildFlowFields();
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
            sources.Remove(position);
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

    private sealed class WaterRedistributionPlan
    {
        private readonly IReadOnlyList<PlanUnit> additions;
        private readonly IReadOnlyList<PlanUnit> removals;
        private readonly bool generatesWater;

        private int additionIndex;
        private int removalIndex;
        private int allowedDistance;

        public int UnitsPerTick { get; }

        public bool IsComplete =>
            additionIndex >= additions.Count &&
            (generatesWater || removalIndex >= removals.Count);

        public WaterRedistributionPlan(
            IReadOnlyList<PlanUnit> additions,
            IReadOnlyList<PlanUnit> removals,
            bool generatesWater,
            int unitsPerTick)
        {
            this.additions = additions;
            this.removals = removals;
            this.generatesWater = generatesWater;
            UnitsPerTick = Mathf.Max(1, unitsPerTick);
        }

        public void AdvanceFlowFront(int depth)
        {
            allowedDistance += depth;
        }

        public bool TryTakeTransfer(
            out Vector3Int source,
            out Vector3Int destination,
            out bool generated)
        {
            source = default;
            destination = default;
            generated = generatesWater;

            if (additionIndex >= additions.Count ||
                additions[additionIndex].Distance > allowedDistance ||
                (!generatesWater && removalIndex >= removals.Count))
            {
                return false;
            }

            destination = additions[additionIndex].Position;
            additionIndex++;

            if (!generatesWater)
            {
                source = removals[removalIndex].Position;
                removalIndex++;
            }

            return true;
        }
    }

    private readonly struct OutletFlow
    {
        public Vector3Int Direction { get; }
        public int Capacity { get; }

        public OutletFlow(
            Vector3Int direction,
            int capacity)
        {
            Direction = direction;
            Capacity = Mathf.Max(1, capacity);
        }
    }

    private readonly struct PlanUnit
    {
        public Vector3Int Position { get; }
        public int Distance { get; }

        public PlanUnit(
            Vector3Int position,
            int distance)
        {
            Position = position;
            Distance = distance;
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
