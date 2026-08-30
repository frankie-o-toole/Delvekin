using System;

[Serializable]
public struct Voxel
{
    public VoxelType Type;

    // Only meaningful for oriented voxel types such as Ladder.
    // For a Ladder this points away from its backing wall.
    public PuzzleSide Facing;

    public Voxel(VoxelType type)
        : this(type, PuzzleSide.North)
    {
    }

    public Voxel(
        VoxelType type,
        PuzzleSide facing)
    {
        Type = type;
        Facing = facing;
    }

    public readonly bool IsSolid() => Type != VoxelType.Air;
}
