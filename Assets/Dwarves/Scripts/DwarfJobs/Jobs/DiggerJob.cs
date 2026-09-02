using System.Collections.Generic;
using UnityEngine;

public sealed class DiggerJob : IDwarfJob
{
    private readonly float cycleDuration;

    private float cycleElapsed;
    private int completedDescents;

    public DwarfJobType Type =>
        DwarfJobType.Digger;

    public bool IsComplete
    {
        get;
        private set;
    }

    public bool ControlsMovement =>
        true;

    public bool CanBeCancelled =>
        true;

    public DiggerJob(
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
                "The Digger is missing required references.";

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
                "The Digger cannot access movement or the voxel world.";

            return false;
        }

        if (context.Movement.State ==
            DwarfMovement.MovementState.Falling)
        {
            failureReason =
                "The Digger must reach stable ground first.";

            return false;
        }

        /*
         * Suitability is deliberately not checked here.
         * The job activates and is consumed before attempting
         * the ground. A bad assignment therefore wastes it.
         */
        failureReason =
            string.Empty;

        return true;
    }

    public void Enter(
        DwarfJobContext context)
    {
        IsComplete = false;
        cycleElapsed = 0f;
        completedDescents = 0;
    }

    public void Tick(
        DwarfJobContext context)
    {
        if (IsComplete)
        {
            return;
        }

        /*
         * Descent time counts as part of the complete cycle.
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

        ExecuteDiggingCycle(
            context);
    }

    public void Exit(
        DwarfJobContext context,
        DwarfJobEndReason reason)
    {
        // No persistent registration requires cleanup.
    }

    private void ExecuteDiggingCycle(
        DwarfJobContext context)
    {
        Vector3Int currentAnchor =
            context.Agent.CurrentVoxel;

        Vector3Int groundLayer =
            currentAnchor +
            Vector3Int.down;

        HashSet<Vector3Int> finishedGroundMask =
            BuildMask(
                groundLayer,
                stage: 3);

        /*
         * The entire eventual rounded opening is validated
         * before Stage 1 touches the layer.
         */
        if (ContainsNonDiggableVoxel(
                context.World,
                finishedGroundMask))
        {
            FailAttempt(
                context,
                "The digging layer contains a non-diggable voxel.");

            return;
        }

        if (!ContainsDiggableMaterial(
                context.World,
                finishedGroundMask))
        {
            FailAttempt(
                context,
                "The digging layer contains no diggable material.");

            return;
        }

        Vector3Int lookaheadLayer =
            groundLayer +
            Vector3Int.down;

        HashSet<Vector3Int> lookaheadMask =
            BuildMask(
                lookaheadLayer,
                stage: 3);

        bool opensIntoAir =
            ContainsOnlyAir(
                context.World,
                lookaheadMask);

        if (opensIntoAir)
        {
            ExecuteFinalStroke(
                context,
                currentAnchor,
                groundLayer);

            return;
        }

        ExecuteNormalStroke(
            context,
            currentAnchor,
            groundLayer);
    }

    private void ExecuteNormalStroke(
        DwarfJobContext context,
        Vector3Int currentAnchor,
        Vector3Int groundLayer)
    {
        HashSet<Vector3Int> excavation =
            new();

        // New ground layer receives Stage 1.
        AddMask(
            excavation,
            groundLayer,
            stage: 1);

        // The layer currently containing the anchor becomes Stage 2.
        if (completedDescents >= 1)
        {
            AddMask(
                excavation,
                currentAnchor,
                stage: 2);
        }

        // The older unfinished layer becomes Stage 3.
        if (completedDescents >= 2)
        {
            AddMask(
                excavation,
                currentAnchor + Vector3Int.up,
                stage: 3);
        }

        if (ContainsNonDiggableVoxel(
                context.World,
                excavation))
        {
            FailAttempt(
                context,
                "An unfinished shaft layer now contains "
                + "a non-diggable voxel.");

            return;
        }

        context.World.SetVoxels(
            excavation,
            VoxelType.Air);

        completedDescents++;

        context.Movement.MoveToVoxel(
            groundLayer,
            DwarfMovement.MovementState.SteppingDown);
    }

    private void ExecuteFinalStroke(
        DwarfJobContext context,
        Vector3Int currentAnchor,
        Vector3Int groundLayer)
    {
        HashSet<Vector3Int> excavation =
            new();

        /*
         * The final solid ground layer receives all three
         * radial stages at once: a finished Stage 3 opening.
         */
        AddMask(
            excavation,
            groundLayer,
            stage: 3);

        /*
         * Complete only layers previously introduced by this job.
         * This prevents the first stroke from removing unrelated
         * terrain beside the dwarf at its original height.
         */
        if (completedDescents >= 1)
        {
            AddMask(
                excavation,
                currentAnchor,
                stage: 3);
        }

        if (completedDescents >= 2)
        {
            AddMask(
                excavation,
                currentAnchor + Vector3Int.up,
                stage: 3);
        }

        if (ContainsNonDiggableVoxel(
                context.World,
                excavation))
        {
            FailAttempt(
                context,
                "The final shaft stroke contains "
                + "a non-diggable voxel.");

            return;
        }

        context.World.SetVoxels(
            excavation,
            VoxelType.Air);

        /*
         * The job is complete before movement resolves.
         * DwarfMovement will descend into the opening and then
         * continue falling if the cavity has no support.
         */
        IsComplete = true;

        context.Movement.MoveToVoxel(
            groundLayer,
            DwarfMovement.MovementState.SteppingDown);
    }

    private void FailAttempt(
        DwarfJobContext context,
        string reason)
    {
        Debug.Log(
            $"[Digger] {context.Agent.name} failed: {reason}",
            context.Agent);

        /*
         * This is an active job, so completion does not refund
         * its inventory token.
         */
        IsComplete = true;
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

    private static bool IsPermittedInShaft(
        VoxelType type)
    {
        return type == VoxelType.Air ||
               IsDiggableMaterial(type);
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

    private static bool ContainsOnlyAir(
        VoxelWorld world,
        IEnumerable<Vector3Int> positions)
    {
        foreach (Vector3Int position in positions)
        {
            if (world.GetVoxel(position).Type !=
                VoxelType.Air)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsNonDiggableVoxel(
        VoxelWorld world,
        IEnumerable<Vector3Int> positions)
    {
        foreach (Vector3Int position in positions)
        {
            VoxelType type =
                world.GetVoxel(position).Type;

            if (!IsPermittedInShaft(type))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<Vector3Int> BuildMask(
        Vector3Int layerCentre,
        int stage)
    {
        HashSet<Vector3Int> result =
            new();

        AddMask(
            result,
            layerCentre,
            stage);

        return result;
    }

    private static void AddMask(
        HashSet<Vector3Int> result,
        Vector3Int layerCentre,
        int stage)
    {
        switch (stage)
        {
            case 1:
                // Central 3x3.
                for (int z = -1; z <= 1; z++)
                {
                    AddRow(
                        result,
                        layerCentre,
                        z,
                        halfWidth: 1);
                }

                break;

            case 2:
                // Rounded 5x5: widths 3, 5, 5, 5, 3.
                AddRow(
                    result,
                    layerCentre,
                    zOffset: -2,
                    halfWidth: 1);

                for (int z = -1; z <= 1; z++)
                {
                    AddRow(
                        result,
                        layerCentre,
                        z,
                        halfWidth: 2);
                }

                AddRow(
                    result,
                    layerCentre,
                    zOffset: 2,
                    halfWidth: 1);

                break;

            case 3:
                // Rounded 7x7: widths 3, 5, 7, 7, 7, 5, 3.
                AddRow(
                    result,
                    layerCentre,
                    zOffset: -3,
                    halfWidth: 1);

                AddRow(
                    result,
                    layerCentre,
                    zOffset: -2,
                    halfWidth: 2);

                for (int z = -1; z <= 1; z++)
                {
                    AddRow(
                        result,
                        layerCentre,
                        z,
                        halfWidth: 3);
                }

                AddRow(
                    result,
                    layerCentre,
                    zOffset: 2,
                    halfWidth: 2);

                AddRow(
                    result,
                    layerCentre,
                    zOffset: 3,
                    halfWidth: 1);

                break;
        }
    }

    private static void AddRow(
        HashSet<Vector3Int> result,
        Vector3Int layerCentre,
        int zOffset,
        int halfWidth)
    {
        for (
            int xOffset = -halfWidth;
            xOffset <= halfWidth;
            xOffset++)
        {
            result.Add(
                layerCentre +
                new Vector3Int(
                    xOffset,
                    0,
                    zOffset));
        }
    }
}
