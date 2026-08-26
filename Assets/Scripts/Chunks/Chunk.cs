using UnityEngine;

public class Chunk
{
    public const int ChunkSize = 16;

    private Voxel[,,] voxels;

    public Vector3Int ChunkCoordinate;

    public Chunk(Vector3Int chunkCoordinate)
    {
        ChunkCoordinate = chunkCoordinate;

        voxels = new Voxel[ChunkSize, ChunkSize, ChunkSize];

        Initialize();
    }

    private void Initialize()
    {
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkSize; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    voxels[x, y, z] = new Voxel(VoxelType.Air);
                }
            }
        }
    }

    public Voxel GetVoxel(int x, int y, int z)
    {
        return voxels[x, y, z];
    }

    public void SetVoxel(int x, int y, int z, Voxel voxel)
    {
        voxels[x, y, z] = voxel;
    }
}