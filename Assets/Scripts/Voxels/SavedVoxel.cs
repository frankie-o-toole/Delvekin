using System;

[Serializable]
[Obsolete("Legacy JSON import only. Use LevelVoxelRecord.")]
public class SavedVoxel
{
    public int x;
    public int y;
    public int z;

    public VoxelType type;

    // Ignored by non-oriented voxel types.
    public PuzzleSide facing;
}
