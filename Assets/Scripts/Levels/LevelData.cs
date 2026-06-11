using System;
using UnityEngine;

[Serializable]
public class LevelData
{
    public int seed;
    public int chunkSize;
    public int worldSizeInChunks;

    public VoxelType[,,][,,] chunks;
}