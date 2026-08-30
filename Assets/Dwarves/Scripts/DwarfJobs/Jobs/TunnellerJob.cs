using System.Collections.Generic;
using UnityEngine;

public sealed class TunnellerJob :
    IDwarfJob,
    IDwarfMovementDecisionJob
{
    private enum TunnellerPhase
    {
        SeekingWall,
        Digging,
        FinishingFirstTail,
        FinishingSecondTail
    }

    private readonly float cycleDuration;

    private PuzzleSide tunnelDirection;
    private Vector3Int forward;
    private Vector3Int sideways;

    private TunnellerPhase phase;

    private float cycleElapsed;
    private int completedAdvances;

    public DwarfJobType Type =>
        DwarfJobType.Tunneller;

    public bool IsComplete
    {
        get;
        private set;
    }

    public bool ControlsMovement =>
        phase != TunnellerPhase.SeekingWall;

    public bool CanBeCancelled =>
        true;

    public TunnellerJob(
        float cycleDuration)
    {
        this.cycleDuration =
            Mathf.Max(
                0.05f,
                cycleDuration);
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
                "The tunneller is missing required references.";

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
                "The tunneller cannot access movement or the voxel world.";

            return false;
        }

        if (context.Movement.State ==
            DwarfMovement.MovementState.Falling)
        {
            failureReason =
                "The tunneller must reach stable ground first.";

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

        tunnelDirection =
            context.Agent.Facing;

        forward =
            DirectionUtility.ToVector(
                tunnelDirection);

        sideways =
            new Vector3Int(
                forward.z,
                0,
                -forward.x);

        phase =
            TunnellerPhase.SeekingWall;

        cycleElapsed = 0f;
        completedAdvances = 0;
    }

    public void Tick(
        DwarfJobContext context)
    {
        if (IsComplete)
        {
            return;
        }

        if (phase == TunnellerPhase.SeekingWall)
        {
            return;
        }

        /*
         * Time spent moving forward is part of the cycle.
         * This keeps cycleDuration representative of the
         * complete tunnel rhythm.
         */
        cycleElapsed +=
            Time.deltaTime;

        if (context.Movement.IsMoving)
        {
            return;
        }

        if (cycleElapsed < cycleDuration)
        {
            return;
        }

        cycleElapsed = 0f;

        switch (phase)
        {
            case TunnellerPhase.Digging:
                ExecuteDiggingCycle(
                    context);
                break;

            case TunnellerPhase.FinishingFirstTail:
                ExecuteFirstTailCycle(
                    context);
                break;

            case TunnellerPhase.FinishingSecondTail:
                ExecuteSecondTailCycle(
                    context);
                break;
        }
    }

    public void Exit(
        DwarfJobContext context,
        DwarfJobEndReason reason)
    {
        // No persistent registration or cleanup is required.
    }

    public bool TryHandleMovementDecision(
    DwarfJobContext context)
    {
        if (IsComplete)
        {
            /*
             * Prevent ordinary movement during the frame between
             * job completion and controller cleanup.
             */
            return true;
        }

        if (phase != TunnellerPhase.SeekingWall)
        {
            return true;
        }

        RefreshSeekingDirection(
            context);

        Vector3Int nextAnchor =
            context.Agent.CurrentVoxel +
            forward;

        Vector3Int cuttingLayer =
            nextAnchor +
            forward;

        HashSet<Vector3Int> immediateOpening =
            BuildMask(
                cuttingLayer,
                stage: 1);

        bool foundDiggableMaterial =
            ContainsDiggableMaterial(
                context.World,
                immediateOpening);

        bool foundNonDiggableMaterial =
            TryFindNonDiggableVoxel(
                context.World,
                immediateOpening,
                out Vector3Int immediateBlockedPosition,
                out VoxelType immediateBlockedType);


        /*
         * A completely open dwarf-sized passage is not a wall.
         * Ordinary walking remains in control.
         */
        if (!foundDiggableMaterial &&
            !foundNonDiggableMaterial)
        {
            Debug.Log(
                "[Tunneller] No wall found. Continue walking.");

            return false;
        }

        /*
         * The dwarf encountered something, but it cannot be dug.
         * Consume/end the active job and prevent movement from
         * turning the dwarf during this frame.
         */
        if (foundNonDiggableMaterial)
        {

            Debug.LogWarning(
                $"[Tunneller] {context.Agent.name} stopped before "
                + $"digging: {immediateBlockedType} at "
                + $"{immediateBlockedPosition} blocks the immediate opening.",
                context.Agent);

            IsComplete = true;
            return true;
        }

        HashSet<Vector3Int> eventualFinishedArch =
            BuildMask(
            cuttingLayer,
            stage: 3);

        /*
         * Even though the immediate 3x5 portion is diggable,
         * the complete arch may contain granite, fluid or another
         * protected material.
         */
        if (TryFindNonDiggableVoxel(
                context.World,
                eventualFinishedArch,
                out Vector3Int blockedPosition,
                out VoxelType blockedType))
        {
            Debug.LogWarning(
                $"[Tunneller] {context.Agent.name} stopped before "
                + $"digging: {blockedType} at {blockedPosition} blocks "
                + "the completed tunnel arch.",
                context.Agent);

            IsComplete = true;
            return true;
        }

        if (!HasAnySupportAt(
                context.World,
                nextAnchor))
        {
            Debug.LogWarning(
                $"[Tunneller] {context.Agent.name} stopped before "
                + $"digging: next anchor {nextAnchor} has no support.",
                context.Agent);

            IsComplete = true;
            return true;
        }

        /*
         * A valid wall was found. From this moment onward the
         * tunneller owns movement and starts its wind-up.
         */
        phase =
            TunnellerPhase.Digging;

        cycleElapsed = 0f;

        return true;
    }

    private void RefreshSeekingDirection(
    DwarfJobContext context)
    {
        tunnelDirection =
            context.Agent.Facing;

        forward =
            DirectionUtility.ToVector(
                tunnelDirection);

        sideways =
            new Vector3Int(
                forward.z,
                0,
                -forward.x);
    }

    private void ExecuteDiggingCycle(
        DwarfJobContext context)
    {
        Vector3Int currentAnchor =
            context.Agent.CurrentVoxel;

        Vector3Int nextAnchor =
            currentAnchor +
            forward;

        Vector3Int cuttingLayer =
            nextAnchor +
            forward;
        /*
         * Validate the eventual finished arch before beginning
         * the narrow opening. This prevents discovering granite
         * only after partially cutting the layer.
         */
        HashSet<Vector3Int> futureFinishedArch =
            BuildMask(
                cuttingLayer,
                stage: 3);

        if (ContainsNonDiggableVoxel(
                context.World,
                futureFinishedArch))
        {
            BeginTailOrComplete();
            return;
        }

        if (!ContainsDiggableMaterial(
                context.World,
                futureFinishedArch))
        {
            // The entire eventual arch is already air.
            BeginTailOrComplete();
            return;
        }

        if (!HasAnySupportAt(
                context.World,
                nextAnchor))
        {
            BeginTailOrComplete();
            return;
        }

        HashSet<Vector3Int> excavation =
            new();

        // New layer beyond the dwarf's leading face.
        AddMask(
            excavation,
            cuttingLayer,
            stage: 1);

        // The previously opened Stage 1 layer becomes Stage 2.
        if (completedAdvances >= 1)
        {
            AddMask(
                excavation,
                nextAnchor,
                stage: 2);
        }

        // The previous Stage 2 layer becomes Stage 3.
        if (completedAdvances >= 2)
        {
            AddMask(
                excavation,
                currentAnchor,
                stage: 3);
        }

        if (ContainsNonDiggableVoxel(
                context.World,
                excavation))
        {
            BeginTailOrComplete();
            return;
        }

        context.World.SetVoxels(
            BuildExcavationTargets(
                context.World,
                excavation),
            VoxelType.Air);

        completedAdvances++;

        context.Movement.MoveToVoxel(
            nextAnchor,
            DwarfMovement.MovementState.Walking);
    }

    private void BeginTailOrComplete()
    {
        if (completedAdvances <= 0)
        {
            IsComplete = true;
            return;
        }

        phase =
            TunnellerPhase.FinishingFirstTail;
    }

    private void ExecuteFirstTailCycle(
        DwarfJobContext context)
    {
        Vector3Int currentAnchor =
    context.Agent.CurrentVoxel;

        HashSet<Vector3Int> excavation =
            new();

        // The newest Stage 1 layer sits at the leading face.
        AddMask(
            excavation,
            currentAnchor + forward,
            stage: 2);

        // The current anchor layer was Stage 2.
        if (completedAdvances >= 2)
        {
            AddMask(
                excavation,
                currentAnchor,
                stage: 3);
        }

        /*
         * If at least two layers were entered, the preceding
         * layer is Stage 2 and must become Stage 3.
         */
        if (completedAdvances >= 2)
        {
            AddMask(
                excavation,
                currentAnchor - forward,
                stage: 3);
        }

        if (ContainsNonDiggableVoxel(
                context.World,
                excavation))
        {
            IsComplete = true;
            return;
        }

        context.World.SetVoxels(
            BuildExcavationTargets(
                context.World,
                excavation),
            VoxelType.Air);

        phase =
            TunnellerPhase.FinishingSecondTail;
    }

    private void ExecuteSecondTailCycle(
        DwarfJobContext context)
    {
        Vector3Int currentAnchor =
            context.Agent.CurrentVoxel;

        HashSet<Vector3Int> excavation =
            BuildMask(
                currentAnchor + forward,
                stage: 3);

        if (ContainsNonDiggableVoxel(
                context.World,
                excavation))
        {
            IsComplete = true;
            return;
        }

        context.World.SetVoxels(
            BuildExcavationTargets(
                context.World,
                excavation),
            VoxelType.Air);

        IsComplete = true;
    }

    private bool HasAnySupportAt(
        VoxelWorld world,
        Vector3Int anchor)
    {
        for (int side = -1; side <= 1; side++)
        {
            for (int depth = -1; depth <= 1; depth++)
            {
                Vector3Int supportPosition =
                    anchor +
                    sideways * side +
                    forward * depth +
                    Vector3Int.down;

                Voxel supportVoxel =
                    world.GetVoxel(
                        supportPosition);

                if (VoxelRules.IsSolid(
                        supportVoxel))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsDiggableMaterial(
        VoxelType type)
    {
        switch (type)
        {
            case VoxelType.Dirt:
            case VoxelType.Vine:
            case VoxelType.Snow:
            case VoxelType.Stair:
            case VoxelType.Ladder:
                return true;

            default:
                return false;
        }
    }

    private static bool IsPermittedInTunnel(
        VoxelType type)
    {
        return type == VoxelType.Air ||
               type == VoxelType.SpawnPoint ||
               IsDiggableMaterial(type);
    }

    private static HashSet<Vector3Int> BuildExcavationTargets(
        VoxelWorld world,
        IEnumerable<Vector3Int> positions)
    {
        HashSet<Vector3Int> targets =
            new();

        foreach (Vector3Int position in positions)
        {
            // SpawnPoint is non-physical level metadata. It permits the
            // tunnel but must survive excavation for later restarts/saves.
            if (world.GetVoxel(position).Type ==
                VoxelType.SpawnPoint)
            {
                continue;
            }

            targets.Add(position);
        }

        return targets;
    }

    private static bool ContainsDiggableMaterial(
        VoxelWorld world,
        IEnumerable<Vector3Int> positions)
    {
        foreach (Vector3Int position in positions)
        {
            if (IsDiggableMaterial(
                    world.GetVoxel(position).Type))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsNonDiggableVoxel(
        VoxelWorld world,
        IEnumerable<Vector3Int> positions)
    {
        return TryFindNonDiggableVoxel(
            world,
            positions,
            out _,
            out _);
    }

    private static bool TryFindNonDiggableVoxel(
        VoxelWorld world,
        IEnumerable<Vector3Int> positions,
        out Vector3Int blockedPosition,
        out VoxelType blockedType)
    {
        foreach (Vector3Int position in positions)
        {
            VoxelType type =
                world.GetVoxel(position).Type;

            if (!IsPermittedInTunnel(type))
            {
                blockedPosition = position;
                blockedType = type;
                return true;
            }
        }

        blockedPosition = default;
        blockedType = VoxelType.Air;
        return false;
    }

    private HashSet<Vector3Int> BuildMask(
        Vector3Int layerAnchor,
        int stage)
    {
        HashSet<Vector3Int> result =
            new();

        AddMask(
            result,
            layerAnchor,
            stage);

        return result;
    }

    private void AddMask(
        HashSet<Vector3Int> result,
        Vector3Int layerAnchor,
        int stage)
    {
        switch (stage)
        {
            case 1:
                AddRectangle(
                    result,
                    layerAnchor,
                    halfWidth: 1,
                    minimumHeight: 0,
                    maximumHeight: 4);
                break;

            case 2:
                // Five bottom rows are five voxels wide.
                AddRectangle(
                    result,
                    layerAnchor,
                    halfWidth: 2,
                    minimumHeight: 0,
                    maximumHeight: 4);

                // Top row is three voxels wide.
                AddRectangle(
                    result,
                    layerAnchor,
                    halfWidth: 1,
                    minimumHeight: 5,
                    maximumHeight: 5);
                break;

            case 3:
                // Five bottom rows are seven voxels wide.
                AddRectangle(
                    result,
                    layerAnchor,
                    halfWidth: 3,
                    minimumHeight: 0,
                    maximumHeight: 4);

                // Penultimate row is five voxels wide.
                AddRectangle(
                    result,
                    layerAnchor,
                    halfWidth: 2,
                    minimumHeight: 5,
                    maximumHeight: 5);

                // Top row is three voxels wide.
                AddRectangle(
                    result,
                    layerAnchor,
                    halfWidth: 1,
                    minimumHeight: 6,
                    maximumHeight: 6);
                break;
        }
    }

    private void AddRectangle(
        HashSet<Vector3Int> result,
        Vector3Int layerAnchor,
        int halfWidth,
        int minimumHeight,
        int maximumHeight)
    {
        for (
            int height = minimumHeight;
            height <= maximumHeight;
            height++)
        {
            for (
                int side = -halfWidth;
                side <= halfWidth;
                side++)
            {
                Vector3Int position =
                    layerAnchor +
                    sideways * side +
                    Vector3Int.up * height;

                result.Add(
                    position);
            }
        }
    }
}
