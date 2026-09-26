using UnityEngine;

public static class VoxelRules
{
    // =========================
    // BASIC TYPE PREDICATES
    // =========================

    public static bool IsAir(Voxel voxel)
    {
        return VoxelTraits.Has(
            voxel.Type,
            VoxelTrait.Empty);
    }

    public static bool IsSolid(Voxel voxel)
    {
        return VoxelTraits.Has(
            voxel.Type,
            VoxelTrait.ProvidesSupport);
    }

    public static bool IsLethal(Voxel voxel)
    {
        return VoxelTraits.Has(
            voxel.Type,
            VoxelTrait.Lethal);
    }

    public static bool IsFluid(Voxel voxel)
    {
        return VoxelTraits.Has(
            voxel.Type,
            VoxelTrait.Fluid);
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
        return VoxelTraits.Has(
            voxel.Type,
            VoxelTrait.BlocksMovement);
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
