using System;

[Serializable]
public class SavedVoxel
{
    public int x;
    public int y;
    public int z;

    public VoxelType type;

    // Water uses two discrete units: 1 = Half and 2 = Full.
    // Missing/zero means Full for compatibility with older level files.
    public byte waterAmount;

    // Ignored by non-oriented voxel types.
    public PuzzleSide facing;
}
