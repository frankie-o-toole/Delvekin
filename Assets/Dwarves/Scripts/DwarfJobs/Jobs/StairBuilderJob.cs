using System.Collections.Generic;
using UnityEngine;

public sealed class StairBuilderJob : IDwarfJob
{
    private enum StairBuilderPhase
    {
        WaitingToBuild,
        MovingOntoStair
    }

    private const int StairWidth = 3;
    private const int StairDepth = 6;
    private const int ForwardOffsetFromAnchor = 2;
    private const int MovementStepsPerPiece = 3;

    private readonly float buildInterval;

    private Vector3Int forward;
    private Vector3Int sideways;

    private StairBuilderPhase phase;
    private float buildElapsed;
    private int movementStep;
    private bool hasPlacedFirstPiece;

    public DwarfJobType Type =>
        DwarfJobType.StairBuilder;

    public bool IsComplete
    {
        get;
        private set;
    }

    public bool ControlsMovement =>
        true;

    public bool CanBeCancelled =>
        true;

    public StairBuilderJob(
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
                "The stair builder is missing required references.";

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
                "The stair builder cannot access movement or the voxel world.";

            return false;
        }

        if (context.Movement.State ==
            DwarfMovement.MovementState.Falling)
        {
            failureReason =
                "The stair builder must reach stable ground first.";

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

        forward =
            DirectionUtility.ToVector(
                context.Agent.Facing);

        sideways =
            new Vector3Int(
                forward.z,
                0,
                -forward.x);

        phase =
            StairBuilderPhase.WaitingToBuild;

        buildElapsed = 0f;
        movementStep = 0;
        hasPlacedFirstPiece = false;
    }

    public void Tick(
        DwarfJobContext context)
    {
        if (IsComplete)
        {
            return;
        }

        if (context.Movement.IsMoving)
        {
            return;
        }

        switch (phase)
        {
            case StairBuilderPhase.WaitingToBuild:
                TickWaitingToBuild(
                    context);
                break;

            case StairBuilderPhase.MovingOntoStair:
                TickMovingOntoStair(
                    context);
                break;
        }
    }

    public void Exit(
        DwarfJobContext context,
        DwarfJobEndReason reason)
    {
        // Stair voxels persist in the world. No cleanup is required.
    }

    private void TickWaitingToBuild(
        DwarfJobContext context)
    {
        buildElapsed +=
            Time.deltaTime;

        if (buildElapsed < buildInterval)
        {
            return;
        }

        HashSet<Vector3Int> stairPiece =
            BuildStairPiece(
                context.Agent.CurrentVoxel);

        if (!CanPlacePiece(
                context.World,
                stairPiece,
                requireTerrainFoundation:
                    !hasPlacedFirstPiece,
                out string failureReason))
        {
            Debug.Log(
                $"[Stair Builder] {context.Agent.name} stopped: "
                + failureReason,
                context.Agent);

            IsComplete = true;
            return;
        }

        context.World.SetVoxels(
            stairPiece,
            VoxelType.Stair);

        hasPlacedFirstPiece = true;
        movementStep = 0;
        phase =
            StairBuilderPhase.MovingOntoStair;
    }

    private void TickMovingOntoStair(
        DwarfJobContext context)
    {
        if (movementStep >=
            MovementStepsPerPiece)
        {
            buildElapsed = 0f;
            phase =
                StairBuilderPhase.WaitingToBuild;

            return;
        }

        bool isFirstMovementStep =
            movementStep == 0;

        Vector3Int targetAnchor =
            context.Agent.CurrentVoxel +
            forward +
            (isFirstMovementStep
                ? Vector3Int.up
                : Vector3Int.zero);

        if (!DwarfWorldQueries.CanOccupy(
                context.World,
                targetAnchor))
        {
            Debug.Log(
                $"[Stair Builder] {context.Agent.name} stopped: "
                + "the dwarf cannot fit at the next stair position.",
                context.Agent);

            IsComplete = true;
            return;
        }

        if (!HasAnySolidSupport(
                context.World,
                targetAnchor))
        {
            Debug.Log(
                $"[Stair Builder] {context.Agent.name} stopped: "
                + "the next stair position has no support.",
                context.Agent);

            IsComplete = true;
            return;
        }

        context.Movement.MoveToVoxel(
            targetAnchor,
            isFirstMovementStep
                ? DwarfMovement.MovementState.SteppingUp
                : DwarfMovement.MovementState.Walking);

        movementStep++;
    }

    private HashSet<Vector3Int> BuildStairPiece(
        Vector3Int dwarfAnchor)
    {
        HashSet<Vector3Int> result =
            new();

        Vector3Int firstLayerCenter =
            dwarfAnchor +
            forward * ForwardOffsetFromAnchor;

        int halfWidth =
            StairWidth / 2;

        for (int depth = 0;
             depth < StairDepth;
             depth++)
        {
            for (int side = -halfWidth;
                 side <= halfWidth;
                 side++)
            {
                result.Add(
                    firstLayerCenter +
                    forward * depth +
                    sideways * side);
            }
        }

        return result;
    }

    private static bool CanPlacePiece(
        VoxelWorld world,
        IEnumerable<Vector3Int> stairPiece,
        bool requireTerrainFoundation,
        out string failureReason)
    {
        bool hasValidTerrainFoundation =
            false;

        foreach (Vector3Int position in stairPiece)
        {
            if (world.GetVoxel(position).Type !=
                VoxelType.Air)
            {
                failureReason =
                    $"stair voxel {position} is occupied by "
                    + $"{world.GetVoxel(position).Type}.";

                return false;
            }

            if (!requireTerrainFoundation)
            {
                continue;
            }

            VoxelType supportType =
                world.GetVoxel(
                    position +
                    Vector3Int.down).Type;

            if (supportType ==
                VoxelType.Lava)
            {
                failureReason =
                    $"Lava beneath {position} would burn the stairs.";

                return false;
            }

            if (IsValidInitialFoundation(
                    supportType))
            {
                hasValidTerrainFoundation =
                    true;
            }
        }

        if (requireTerrainFoundation &&
            !hasValidTerrainFoundation)
        {
            failureReason =
                "the first piece has no Dirt, Granite, Snow or Vine foundation.";

            return false;
        }

        failureReason =
            string.Empty;

        return true;
    }

    private static bool IsValidInitialFoundation(
        VoxelType type)
    {
        switch (type)
        {
            case VoxelType.Dirt:
            case VoxelType.Granite:
            case VoxelType.Snow:
            case VoxelType.Vine:
                return true;

            default:
                return false;
        }
    }

    private bool HasAnySolidSupport(
        VoxelWorld world,
        Vector3Int anchor)
    {
        for (int side = -1;
             side <= 1;
             side++)
        {
            for (int depth = -1;
                 depth <= 1;
                 depth++)
            {
                Vector3Int supportPosition =
                    anchor +
                    sideways * side +
                    forward * depth +
                    Vector3Int.down;

                if (VoxelRules.IsSolid(
                        world.GetVoxel(
                            supportPosition)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
