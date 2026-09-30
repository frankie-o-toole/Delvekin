using System;
using System.Collections.Generic;

[Serializable]
[Obsolete("Legacy JSON import only. Use LevelSaveData.")]
public class SavedLevel
{
    public List<SavedVoxel> voxels = new();
}
