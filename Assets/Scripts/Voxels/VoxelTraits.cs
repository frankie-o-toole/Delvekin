using System;

[Flags]
public enum VoxelTrait
{
    None = 0,
    Empty = 1 << 0,
    ProvidesSupport = 1 << 1,
    BlocksMovement = 1 << 2,
    Lethal = 1 << 3,
    Fluid = 1 << 4,
    AllowsDwarfClearance = 1 << 5,
    Climbable = 1 << 6,
    GameplayMarker = 1 << 7,
    Diggable = 1 << 8,
    StairFoundation = 1 << 9,
    TerrainRenderable = 1 << 10,
    FluidRenderable = 1 << 11
}

/// <summary>
/// Single source of truth for material behaviour shared by gameplay systems.
/// Checks for a specific identity (for example Water rather than any fluid)
/// should still compare VoxelType directly.
/// </summary>
public static class VoxelTraits
{
    public static VoxelTrait Get(VoxelType type)
    {
        switch (type)
        {
            case VoxelType.Air:
                return VoxelTrait.Empty |
                       VoxelTrait.AllowsDwarfClearance;

            case VoxelType.Dirt:
                return SolidTerrain() |
                       VoxelTrait.Diggable |
                       VoxelTrait.StairFoundation;

            case VoxelType.Granite:
                return SolidTerrain() |
                       VoxelTrait.StairFoundation;

            case VoxelType.Lava:
                return VoxelTrait.Lethal |
                       VoxelTrait.Fluid |
                       VoxelTrait.FluidRenderable;

            case VoxelType.Water:
                return VoxelTrait.Fluid |
                       VoxelTrait.AllowsDwarfClearance |
                       VoxelTrait.FluidRenderable;

            case VoxelType.Vine:
                return SolidTerrain() |
                       VoxelTrait.Climbable |
                       VoxelTrait.Diggable |
                       VoxelTrait.StairFoundation;

            case VoxelType.Snow:
                return SolidTerrain() |
                       VoxelTrait.Diggable |
                       VoxelTrait.StairFoundation;

            case VoxelType.Bubblegum:
                return SolidTerrain();

            case VoxelType.SpawnPoint:
            case VoxelType.ExitPoint:
                return VoxelTrait.GameplayMarker |
                       VoxelTrait.AllowsDwarfClearance;

            case VoxelType.Stair:
                return SolidTerrain() |
                       VoxelTrait.Diggable;

            case VoxelType.Ladder:
                return VoxelTrait.BlocksMovement |
                       VoxelTrait.Climbable |
                       VoxelTrait.Diggable |
                       VoxelTrait.TerrainRenderable;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(type),
                    type,
                    "VoxelType has no registered traits.");
        }
    }

    public static bool Has(
        VoxelType type,
        VoxelTrait trait)
    {
        return (Get(type) & trait) == trait;
    }

    private static VoxelTrait SolidTerrain()
    {
        return VoxelTrait.ProvidesSupport |
               VoxelTrait.BlocksMovement |
               VoxelTrait.TerrainRenderable;
    }
}
