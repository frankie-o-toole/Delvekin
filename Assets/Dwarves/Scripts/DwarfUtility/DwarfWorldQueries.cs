using UnityEngine;

/// <summary>
/// Shared voxel-world queries for dwarf spawning and movement.
/// </summary>
public static class DwarfWorldQueries
{
    public static bool CanOccupy(
        VoxelWorld world,
        Vector3Int anchorVoxel)
    {
        return CanOccupy(
            world,
            anchorVoxel,
            out _);
    }

    public static bool CanOccupy(
        VoxelWorld world,
        Vector3Int anchorVoxel,
        out Vector3Int blockedVoxel)
    {
        foreach (Vector3Int voxelPosition
                 in DwarfSpatialRules.GetOccupiedVoxels(anchorVoxel))
        {
            VoxelType type =
                world.GetVoxel(voxelPosition).Type;

            if (!IsClearanceVoxel(type))
            {
                blockedVoxel = voxelPosition;
                return false;
            }
        }

        blockedVoxel = default;
        return true;
    }

    /// <summary>
    /// Returns true when at least one of the nine cells underneath the
    /// dwarf contains supportive terrain.
    ///
    /// The dwarf only falls after its complete footprint has left the edge.
    /// </summary>
    public static bool HasAnySupport(
        VoxelWorld world,
        Vector3Int anchorVoxel)
    {
        foreach (Vector3Int supportVoxel
                 in DwarfSpatialRules.GetSupportVoxels(anchorVoxel))
        {
            VoxelType type =
                world.GetVoxel(supportVoxel).Type;

            if (IsSupportive(type))
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasNoSupport(
        VoxelWorld world,
        Vector3Int anchorVoxel)
    {
        return !HasAnySupport(
            world,
            anchorVoxel);
    }

    /// <summary>
    /// Retained for strict spawn validation.
    /// </summary>
    public static bool HasCentreSupport(
        VoxelWorld world,
        Vector3Int anchorVoxel)
    {
        Vector3Int centreSupport =
            DwarfSpatialRules.GetCentreSupportVoxel(anchorVoxel);

        VoxelType type =
            world.GetVoxel(centreSupport).Type;

        return IsSupportive(type);
    }

    public static int CountSupportVoxels(
        VoxelWorld world,
        Vector3Int anchorVoxel)
    {
        int count = 0;

        foreach (Vector3Int supportVoxel
                 in DwarfSpatialRules.GetSupportVoxels(anchorVoxel))
        {
            VoxelType type =
                world.GetVoxel(supportVoxel).Type;

            if (IsSupportive(type))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Determines whether the proposed forward volume is blocked only
    /// along its bottom layer.
    ///
    /// That obstruction represents a traversable one-voxel rise.
    /// Any obstruction above the bottom layer is a wall or low ceiling.
    /// </summary>
    public static bool IsOneVoxelRise(
        VoxelWorld world,
        Vector3Int forwardAnchor)
    {
        bool foundBottomObstruction = false;

        foreach (Vector3Int voxelPosition
                 in DwarfSpatialRules.GetOccupiedVoxels(forwardAnchor))
        {
            VoxelType type =
                world.GetVoxel(voxelPosition).Type;

            if (IsClearanceVoxel(type))
            {
                continue;
            }

            // Ladder is never treated as a one-voxel stair. It is either
            // entered through the directional climbing rule or blocks movement.
            if (type == VoxelType.Ladder)
            {
                return false;
            }

            if (voxelPosition.y != forwardAnchor.y)
            {
                return false;
            }

            foundBottomObstruction = true;
        }

        return foundBottomObstruction;
    }

    public static bool IsClearanceVoxel(
        VoxelType type)
    {
        return
            type == VoxelType.Air ||
            type == VoxelType.SpawnPoint;
    }

    public static bool IsSupportive(
        VoxelType type)
    {
        return
            type == VoxelType.Dirt ||
            type == VoxelType.Granite ||
            type == VoxelType.Snow ||
            type == VoxelType.Vine ||
            type == VoxelType.Stair;
    }

    /// <summary>
    /// Counts correctly oriented Ladder voxels touching the dwarf's
    /// three-wide face. The dwarf remains outside the solid Ladder volume.
    /// </summary>
    public static int CountLadderContact(
        VoxelWorld world,
        Vector3Int dwarfAnchor,
        PuzzleSide ladderOutwardSide,
        int verticalOffset = 0)
    {
        Vector3Int outward =
            DirectionUtility.ToVector(
                ladderOutwardSide);

        Vector3Int inward =
            -outward;

        Vector3Int sideways =
            new(
                inward.z,
                0,
                -inward.x);

        Vector3Int ladderCentre =
            dwarfAnchor +
            inward * 2 +
            Vector3Int.up * verticalOffset;

        int count = 0;

        for (int side = -1;
             side <= 1;
             side++)
        {
            Voxel voxel =
                world.GetVoxel(
                    ladderCentre +
                    sideways * side);

            if (voxel.Type == VoxelType.Ladder &&
                voxel.Facing == ladderOutwardSide)
            {
                count++;
            }
        }

        return count;
    }

    public static bool HasClimbableLadderContact(
        VoxelWorld world,
        Vector3Int dwarfAnchor,
        PuzzleSide ladderOutwardSide,
        int verticalOffset = 0)
    {
        return CountLadderContact(
                   world,
                   dwarfAnchor,
                   ladderOutwardSide,
                   verticalOffset) >= 2;
    }
}
