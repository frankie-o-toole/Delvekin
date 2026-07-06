using UnityEngine;

public static class VoxelRules
{
    // =========================
    // BASIC TYPE PREDICATES
    // =========================

    public static bool IsAir(Voxel voxel)
    {
        return voxel.Type == VoxelType.Air;
    }

    public static bool IsSolid(Voxel voxel)
    {
        // Default rule: anything that is not air or fluid is solid support
        switch (voxel.Type)
        {
            case VoxelType.Air:
            case VoxelType.Water:
            case VoxelType.Lava:
                return false;

            default:
                return true;
        }
    }

    public static bool IsLethal(Voxel voxel)
    {
        switch (voxel.Type)
        {
            case VoxelType.Lava:
                return true;

            default:
                return false;
        }
    }

    public static bool IsFluid(Voxel voxel)
    {
        switch (voxel.Type)
        {
            case VoxelType.Water:
            case VoxelType.Lava:
                return true;

            default:
                return false;
        }
    }

    // =========================
    // MOVEMENT RULES
    // =========================

    /// <summary>
    /// Can a dwarf occupy this voxel space?
    /// This is the core movement gate.
    /// </summary>
    public static bool IsBlocked(Voxel voxel)
    {
        // Anything solid blocks movement
        return IsSolid(voxel);
    }

    /// <summary>
    /// Can the dwarf stand safely in this voxel?
    /// Note: this does NOT include support checks.
    /// </summary>
    public static bool IsWalkable(Voxel voxel)
    {
        if (IsBlocked(voxel))
            return false;

        if (IsLethal(voxel))
            return false;

        return true;
    }

    /// <summary>
    /// Determines if entering this voxel causes immediate death.
    /// </summary>
    public static bool CausesDeath(Voxel voxel)
    {
        return IsLethal(voxel);
    }

    /// <summary>
    /// Determines if voxel behaves like a fluid (affects movement style).
    /// </summary>
    public static bool AffectsMovementAsFluid(Voxel voxel)
    {
        return IsFluid(voxel);
    }
}