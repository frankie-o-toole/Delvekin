using System;

[Serializable]
public struct Voxel
{
    public VoxelType Type;

    public Voxel(VoxelType type)
    {
        Type = type;
    }

    public readonly bool IsSolid() => Type != VoxelType.Air;
}
