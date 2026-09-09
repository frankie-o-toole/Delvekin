using System;

[Serializable]
public class SavedVoxel
{
    public int x;
    public int y;
    public int z;

    public VoxelType type;

    // Ignored by non-oriented voxel types.
    public PuzzleSide facing;
}
