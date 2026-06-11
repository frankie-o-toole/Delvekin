using System.Collections.Generic;
using UnityEngine;

public class VoxelWorld : MonoBehaviour
{
    private Dictionary<Vector3Int, Chunk> chunks = new();
    private Dictionary<Vector3Int, ChunkRenderer> chunkRenderers = new();

    public Material voxelMaterial;

    private LevelData currentLevel;

    private void Start()
    {
        LoadGeneratedLevel(1234, 1);
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            LoadGeneratedLevel(Random.Range(0, 99999), 1);
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            LevelSerializer.Save(currentLevel, "test_level");
        }
    }
    public void LoadGeneratedLevel(int seed, int worldSize)
    {
        currentLevel = LevelGenerator.Generate(seed, worldSize);

        BuildFromLevel(currentLevel);
    }

    private void BuildFromLevel(LevelData data)
    {
        for (int x = 0; x < data.worldSizeInChunks; x++)
            for (int z = 0; z < data.worldSizeInChunks; z++)
            {
                Chunk chunk = new Chunk(new Vector3Int(x, 0, z));

                for (int lx = 0; lx < Chunk.ChunkSize; lx++)
                    for (int ly = 0; ly < Chunk.ChunkSize; ly++)
                        for (int lz = 0; lz < Chunk.ChunkSize; lz++)
                        {
                            chunk.SetVoxel(lx, ly, lz,
                                new Voxel(data.chunks[x, 0, z][lx, ly, lz]));
                        }

                chunks[new Vector3Int(x, 0, z)] = chunk;

                CreateChunkRenderer(chunk);
            }
    }

    private void CreateChunkRenderer(Chunk chunk)
    {
        GameObject go = new GameObject($"Chunk {chunk.ChunkCoordinate}");

        ChunkRenderer renderer = go.AddComponent<ChunkRenderer>();

        MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
        meshRenderer.material = voxelMaterial;

        renderer.Initialize(chunk);

        chunkRenderers.Add(chunk.ChunkCoordinate, renderer);
    }

    public Voxel GetVoxel(Vector3Int worldPos)
    {
        Vector3Int chunkCoord = VoxelMath.WorldToChunkCoord(worldPos);
        Vector3Int local = VoxelMath.WorldToLocalVoxel(worldPos);

        if (chunks.TryGetValue(chunkCoord, out Chunk chunk))
        {
            return chunk.GetVoxel(local.x, local.y, local.z);
        }

        return new Voxel(VoxelType.Air);
    }

    public void SetVoxel(Vector3Int worldPos, VoxelType type)
    {
        Vector3Int chunkCoord =
            VoxelMath.WorldToChunkCoord(worldPos);

        Vector3Int localPos =
            VoxelMath.WorldToLocalVoxel(worldPos);

        if (!chunks.TryGetValue(chunkCoord, out Chunk chunk))
            return;

        chunk.SetVoxel(
            localPos.x,
            localPos.y,
            localPos.z,
            new Voxel(type));

        chunkRenderers[chunkCoord].RebuildMesh();
    }
}