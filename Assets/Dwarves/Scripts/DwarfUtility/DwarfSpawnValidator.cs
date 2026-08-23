using UnityEngine;

/// <summary>
/// Validates terrain around a dwarf spawnpoint.
///
/// Other dwarves are deliberately ignored. Dwarves do not collide,
/// so multiple dwarves may occupy or pass through the spawn area.
/// </summary>
public static class DwarfSpawnValidator
{
    public const int RequiredSupportVoxels = 5;

    public static bool CanSpawn(
        VoxelWorld world,
        Vector3Int anchorVoxel,
        out string failureReason)
    {
        if (world == null)
        {
            failureReason =
                "VoxelWorld reference is missing.";

            return false;
        }

        if (!DwarfWorldQueries.CanOccupy(
                world,
                anchorVoxel,
                out Vector3Int blockedVoxel))
        {
            failureReason =
                $"Dwarf clearance is blocked at {blockedVoxel}.";

            return false;
        }

        if (!HasSufficientSupport(
                world,
                anchorVoxel,
                out int supportCount))
        {
            failureReason =
                $"Spawnpoint has insufficient support: "
                + $"{supportCount}/9 supporting voxels.";

            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    public static bool HasSufficientSupport(
        VoxelWorld world,
        Vector3Int anchorVoxel,
        out int supportCount)
    {
        if (!DwarfWorldQueries.HasCentreSupport(
                world,
                anchorVoxel))
        {
            supportCount =
                DwarfWorldQueries.CountSupportVoxels(
                    world,
                    anchorVoxel);

            return false;
        }

        supportCount =
            DwarfWorldQueries.CountSupportVoxels(
                world,
                anchorVoxel);

        return supportCount >=
               RequiredSupportVoxels;
    }
}