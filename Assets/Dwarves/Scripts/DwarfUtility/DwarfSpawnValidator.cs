using UnityEngine;

/// <summary>
/// Validates clearance around a dwarf spawnpoint and predicts its fall.
///
/// Other dwarves are deliberately ignored. Dwarves do not collide,
/// so multiple dwarves may occupy or pass through the spawn area.
/// </summary>
public static class DwarfSpawnValidator
{
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

        failureReason = string.Empty;
        return true;
    }

    /// <summary>
    /// Finds the first anchor below the spawnpoint where the dwarf would
    /// encounter support. The result uses the same footprint support rule as
    /// live movement, so warning behavior stays aligned with gameplay.
    /// </summary>
    public static bool TryGetLandingDistance(
        VoxelWorld world,
        Vector3Int anchorVoxel,
        out int fallDistance)
    {
        fallDistance = 0;

        if (world == null ||
            !world.TryGetVerticalBounds(
                out int minimumWorldY,
                out _))
        {
            return false;
        }

        Vector3Int candidateAnchor =
            anchorVoxel;

        while (candidateAnchor.y > minimumWorldY)
        {
            if (DwarfWorldQueries.HasAnySupport(
                    world,
                    candidateAnchor))
            {
                fallDistance =
                    anchorVoxel.y - candidateAnchor.y;

                return true;
            }

            candidateAnchor +=
                Vector3Int.down;
        }

        return false;
    }
}
