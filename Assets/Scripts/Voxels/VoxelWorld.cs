using System.Collections.Generic;
using UnityEngine;

public class VoxelWorld : MonoBehaviour
{
    private Dictionary<Vector3Int, Chunk> chunks =
        new Dictionary<Vector3Int, Chunk>();

    private Dictionary<Vector3Int, ChunkRenderer> chunkRenderers =
        new Dictionary<Vector3Int, ChunkRenderer>();

    [Header("World Settings")]
    public int worldSizeInChunks = 1;

    public Material voxelMaterial;
    private void Start()
    {
        GenerateTestWorld();
    }

    #region World Generation

    private void GenerateTestWorld()
    {
        for (int x = 0; x < worldSizeInChunks; x++)
            for (int y = 0; y < 1; y++)
                for (int z = 0; z < worldSizeInChunks; z++)
                {
                    Vector3Int chunkCoord = new Vector3Int(x, y, z);

                    CreateChunk(chunkCoord);
                }
    }

    private void CreateChunk(Vector3Int coord)
    {
        // 1. Create data chunk
        Chunk chunk = new Chunk(coord);
        chunks.Add(coord, chunk);

        // 2. Fill test terrain (TEMPORARY PLACEHOLDER)
        FillTestTerrain(chunk);

        // 3. Create renderer
        CreateChunkRenderer(chunk);
    }

    private void FillTestTerrain(Chunk chunk)
    {
        for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int z = 0; z < Chunk.ChunkSize; z++)
            {
                chunk.SetVoxel(x, 0, z, new Voxel(VoxelType.Granite));
                chunk.SetVoxel(x, 1, z, new Voxel(VoxelType.Dirt));
            }
    }
    private void CreateChunkRenderer(Chunk chunk)
    {
        GameObject go = new GameObject($"Chunk {chunk.ChunkCoordinate}");

        ChunkRenderer renderer = go.AddComponent<ChunkRenderer>();

        var meshRenderer = go.GetComponent<MeshRenderer>();
        meshRenderer.material = voxelMaterial;

        renderer.Initialize(chunk);

        chunkRenderers.Add(chunk.ChunkCoordinate, renderer);
    }

    #endregion

    #region World Queries

    public Voxel GetVoxel(Vector3Int worldPos)
    {
        Vector3Int chunkCoord =
            VoxelMath.WorldToChunkCoord(worldPos);

        Vector3Int localPos =
            VoxelMath.WorldToLocalVoxel(worldPos);

        if (chunks.TryGetValue(chunkCoord, out Chunk chunk))
        {
            return chunk.GetVoxel(
                localPos.x,
                localPos.y,
                localPos.z
            );
        }

        return new Voxel(VoxelType.Air);
    }

    public void SetVoxel(Vector3Int worldPos, VoxelType type)
    {
        Vector3Int chunkCoord =
            VoxelMath.WorldToChunkCoord(worldPos);

        Vector3Int localPos =
            VoxelMath.WorldToLocalVoxel(worldPos);

        if (chunks.TryGetValue(chunkCoord, out Chunk chunk))
        {
            chunk.SetVoxel(
                localPos.x,
                localPos.y,
                localPos.z,
                new Voxel(type)
            );

            chunkRenderers[chunkCoord].RebuildMesh();
        }
    }
    #endregion
}