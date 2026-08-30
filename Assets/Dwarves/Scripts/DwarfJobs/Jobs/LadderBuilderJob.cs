using System.Collections.Generic;
using UnityEngine;

public sealed class LadderBuilderJob :
    IDwarfJob,
    IDwarfMovementDecisionJob
{
    private enum LadderBuilderPhase
    {
        SeekingWall,
        BuildingAndClimbing
    }

    private const int RequiredBackingVoxels = 2;
    private const int RequiredExistingLadderVoxels = 2;

    private readonly float buildInterval;

    private LadderBuilderPhase phase;
    private Vector3Int inward;
    private Vector3Int sideways;
    private PuzzleSide outwardSide;
    private float buildElapsed;
    private bool hasBaseLayer;

    public DwarfJobType Type =>
        DwarfJobType.LadderBuilder;

    public bool IsComplete
    {
        get;
        private set;
    }

    public bool ControlsMovement =>
        phase != LadderBuilderPhase.SeekingWall;

    public bool CanBeCancelled =>
        true;

    public LadderBuilderJob(
        float buildInterval)
    {
        this.buildInterval =
            Mathf.Max(
                0.05f,
                buildInterval);
    }

    public bool CanAssign(
        DwarfJobContext context,
        out string failureReason)
    {
        if (context == null ||
            context.Agent == null ||
            context.World == null ||
            context.Movement == null)
        {
            failureReason =
                "The Ladder Builder is missing required references.";

            return false;
        }

        failureReason =
            string.Empty;

        return true;
    }

    public bool CanActivate(
        DwarfJobContext context,
        out string failureReason)
    {
        if (context == null ||
            context.Agent == null ||
            !context.Agent.IsActive)
        {
            failureReason =
                "The dwarf is not active.";

            return false;
        }

        if (context.World == null ||
            context.Movement == null)
        {
            failureReason =
                "The Ladder Builder cannot access movement or the voxel world.";

            return false;
        }

        if (context.Movement.State ==
            DwarfMovement.MovementState.Falling)
        {
            failureReason =
                "The Ladder Builder must reach stable ground first.";

            return false;
        }

        failureReason =
            string.Empty;

        return true;
    }

    public void Enter(
        DwarfJobContext context)
    {
        IsComplete = false;
        phase = LadderBuilderPhase.SeekingWall;
        buildElapsed = 0f;
        hasBaseLayer = false;

        RefreshDirection(context);
    }

    public void Tick(
        DwarfJobContext context)
    {
        if (IsComplete ||
            phase == LadderBuilderPhase.SeekingWall ||
            context.Movement.IsMoving)
        {
            return;
        }

        int layerOffset =
            hasBaseLayer ? 1 : 0;

        if (CountExistingLadderVoxels(
                context.World,
                context.Agent.CurrentVoxel,
                layerOffset) >=
            RequiredExistingLadderVoxels)
        {
            CompleteLayer(
                context,
                layerOffset);

            return;
        }

        buildElapsed +=
            Time.deltaTime;

        if (buildElapsed < buildInterval)
        {
            return;
        }

        buildElapsed = 0f;

        if (!CanBuildLayer(
                context.World,
                context.Agent.CurrentVoxel,
                layerOffset,
                out string failureReason))
        {
            FinishBuilding(
                context,
                failureReason);

            return;
        }

        context.World.SetVoxels(
            BuildLadderLayer(
                context.Agent.CurrentVoxel,
                layerOffset),
            VoxelType.Ladder,
            outwardSide);

        CompleteLayer(
            context,
            layerOffset);
    }

    public void Exit(
        DwarfJobContext context,
        DwarfJobEndReason reason)
    {
        // Constructed Ladder voxels persist in the world. Cancelling while
        // already climbing must still hand the dwarf to safe traversal.
        if (reason == DwarfJobEndReason.Cancelled &&
            phase == LadderBuilderPhase.BuildingAndClimbing &&
            context?.Movement != null)
        {
            context.Movement.FinishLadderBuilding(
                outwardSide);
        }
    }

    public bool TryHandleMovementDecision(
        DwarfJobContext context)
    {
        if (IsComplete)
        {
            return true;
        }

        if (phase != LadderBuilderPhase.SeekingWall)
        {
            return true;
        }

        RefreshDirection(context);

        Vector3Int currentAnchor =
            context.Agent.CurrentVoxel;

        bool foundExistingLadder =
            CountExistingLadderVoxels(
                context.World,
                currentAnchor,
                0) >=
            RequiredExistingLadderVoxels;

        bool foundBuildableWall =
            CanBuildLayer(
                context.World,
                currentAnchor,
                0,
                out _);

        if (!foundExistingLadder &&
            !foundBuildableWall)
        {
            // Keep ordinary walking in control until a suitable wall or
            // compatible environmental Ladder is found.
            return false;
        }

        phase =
            LadderBuilderPhase.BuildingAndClimbing;

        buildElapsed = 0f;

        return true;
    }

    private void RefreshDirection(
        DwarfJobContext context)
    {
        inward =
            DirectionUtility.ToVector(
                context.Agent.Facing);

        sideways =
            new Vector3Int(
                inward.z,
                0,
                -inward.x);

        outwardSide =
            DirectionUtility.Opposite(
                context.Agent.Facing);
    }

    private void CompleteLayer(
        DwarfJobContext context,
        int layerOffset)
    {
        // The first layer is constructed at the grounded anchor. From then
        // on, construct the layer above first and only then climb into it.
        if (!hasBaseLayer &&
            layerOffset == 0)
        {
            hasBaseLayer = true;
            buildElapsed = 0f;
            return;
        }

        Vector3Int upwardAnchor =
            context.Agent.CurrentVoxel +
            Vector3Int.up;

        if (!DwarfWorldQueries.CanOccupy(
                context.World,
                upwardAnchor))
        {
            FinishBuilding(
                context,
                "the dwarf cannot fit above the current ladder layer.");

            return;
        }

        context.Movement.MoveToVoxel(
            upwardAnchor,
            DwarfMovement.MovementState.ClimbingUp);
    }

    private void FinishBuilding(
        DwarfJobContext context,
        string reason)
    {
        Debug.Log(
            $"[Ladder Builder] {context.Agent.name} stopped: {reason}",
            context.Agent);

        context.Movement.FinishLadderBuilding(
            outwardSide);

        IsComplete = true;
    }

    private bool CanBuildLayer(
        VoxelWorld world,
        Vector3Int dwarfAnchor,
        int verticalOffset,
        out string failureReason)
    {
        int validBackingCount = 0;

        for (int side = -1;
             side <= 1;
             side++)
        {
            Vector3Int ladderPosition =
                GetLadderCentre(
                    dwarfAnchor,
                    verticalOffset) +
                sideways * side;

            Vector3Int backingPosition =
                ladderPosition +
                inward;

            if (!world.ContainsExistingChunkAt(
                    ladderPosition) ||
                !world.ContainsExistingChunkAt(
                    backingPosition))
            {
                failureReason =
                    $"ladder layer at {ladderPosition} reaches "
                    + "outside the current world bounds.";

                return false;
            }

            Voxel ladderVoxel =
                world.GetVoxel(
                    ladderPosition);

            if (ladderVoxel.Type ==
                VoxelType.Lava)
            {
                failureReason =
                    $"Lava occupies ladder position {ladderPosition}.";

                return false;
            }

            bool compatibleExistingLadder =
                ladderVoxel.Type ==
                    VoxelType.Ladder &&
                ladderVoxel.Facing ==
                    outwardSide;

            if (ladderVoxel.Type != VoxelType.Air &&
                !compatibleExistingLadder)
            {
                failureReason =
                    $"ladder position {ladderPosition} is occupied by "
                    + $"{ladderVoxel.Type}.";

                return false;
            }

            VoxelType backingType =
                world.GetVoxel(
                    backingPosition).Type;

            if (backingType ==
                VoxelType.Lava)
            {
                failureReason =
                    $"Lava behind {ladderPosition} prevents construction.";

                return false;
            }

            if (IsValidBackingMaterial(
                    backingType))
            {
                validBackingCount++;
            }
        }

        if (validBackingCount <
            RequiredBackingVoxels)
        {
            failureReason =
                $"only {validBackingCount}/3 backing voxels are solid.";

            return false;
        }

        failureReason =
            string.Empty;

        return true;
    }

    private int CountExistingLadderVoxels(
        VoxelWorld world,
        Vector3Int dwarfAnchor,
        int verticalOffset)
    {
        int count = 0;

        foreach (Vector3Int position
                 in BuildLadderLayer(
                     dwarfAnchor,
                     verticalOffset))
        {
            Voxel voxel =
                world.GetVoxel(position);

            if (voxel.Type == VoxelType.Ladder &&
                voxel.Facing == outwardSide)
            {
                count++;
            }
        }

        return count;
    }

    private HashSet<Vector3Int> BuildLadderLayer(
        Vector3Int dwarfAnchor,
        int verticalOffset)
    {
        HashSet<Vector3Int> result =
            new();

        Vector3Int centre =
            GetLadderCentre(
                dwarfAnchor,
                verticalOffset);

        for (int side = -1;
             side <= 1;
             side++)
        {
            result.Add(
                centre +
                sideways * side);
        }

        return result;
    }

    private Vector3Int GetLadderCentre(
        Vector3Int dwarfAnchor,
        int verticalOffset)
    {
        // The dwarf occupies through forward offset +1. Ladder occupies
        // offset +2, and the backing wall begins at offset +3.
        return dwarfAnchor +
               inward * 2 +
               Vector3Int.up * verticalOffset;
    }

    private static bool IsValidBackingMaterial(
        VoxelType type)
    {
        // New solid terrain types should support ladders automatically.
        // SpawnPoint is traversable metadata rather than physical backing.
        if (type == VoxelType.SpawnPoint)
        {
            return false;
        }

        return VoxelRules.IsSolid(
            new Voxel(type));
    }
}
