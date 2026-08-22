using System;
using UnityEngine;

[Serializable]
public class LevelData
{
    public int seed;
    public int chunkSize;

    public int widthInChunks;
    public int heightInChunks;
    public int depthInChunks;

    public VoxelType[,,][,,] chunks;
}