using UnityEngine;

public static class VoxelMath
{
    public static Vector3Int WorldToChunkCoord(Vector3Int worldPos)
    {
        return new Vector3Int(
            Mathf.FloorToInt((float)worldPos.x / Chunk.ChunkSize),
            Mathf.FloorToInt((float)worldPos.y / Chunk.ChunkSize),
            Mathf.FloorToInt((float)worldPos.z / Chunk.ChunkSize)
        );
    }

    public static Vector3Int WorldToLocalVoxel(Vector3Int worldPos)
    {
        return new Vector3Int(
            Mod(worldPos.x, Chunk.ChunkSize),
            Mod(worldPos.y, Chunk.ChunkSize),
            Mod(worldPos.z, Chunk.ChunkSize)
        );
    }

    private static int Mod(int value, int size)
    {
        int result = value % size;

        if (result < 0)
            result += size;

        return result;
    }
}