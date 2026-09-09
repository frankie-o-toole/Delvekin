using System;
using System.Collections.Generic;

[Serializable]
public class SavedLevel
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public List<SavedVoxel> voxels = new();
    public List<SavedWaterSource> waterSources = new();
}