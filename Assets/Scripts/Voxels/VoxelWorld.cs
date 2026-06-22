using System.Collections.Generic;
using UnityEngine;

public class VoxelWorld : MonoBehaviour
{
    private Dictionary<Vector3Int, Chunk> chunks = new();
    private Dictionary<Vector3Int, ChunkRenderer> chunkRenderers = new();

    public Material voxelMaterial;

    private LevelData currentLevel;
    private string fileName = "TestLevel";

    private void Start()
    {
        LoadGeneratedLevel(1234, 1);
    }
    private void Update()
    {
/*        if (Input.GetKeyDown(KeyCode.G))
        {
            LoadGeneratedLevel(Random.Range(0, 99999), 1);
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            LoadSavedLevel("TestLevel");
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            SavedLevel save =
                CreateSaveData();

            LevelSerializer.Save(
                save,
                "TestLevel");
        }*/
    }
    public SavedLevel CreateSaveData()
    {
        SavedLevel save = new();

        foreach (var pair in chunks)
        {
            Vector3Int chunkCoord = pair.Key;
            Chunk chunk = pair.Value;

            for (int x = 0; x < Chunk.ChunkSize; x++)
            {
                for (int y = 0; y < Chunk.ChunkSize; y++)
                {
                    for (int z = 0; z < Chunk.ChunkSize; z++)
                    {
                        Voxel voxel = chunk.GetVoxel(x, y, z);

                        if (voxel.Type == VoxelType.Air)
                            continue;

                        save.voxels.Add(new SavedVoxel
                        {
                            x = chunkCoord.x * Chunk.ChunkSize + x,
                            y = y,
                            z = chunkCoord.z * Chunk.ChunkSize + z,
                            type = voxel.Type
                        });
                    }
                }
            }
        }

        return save;
    }
    public void SaveLevel(string name)
    {
        SavedLevel save = CreateSaveData();

        LevelSerializer.Save(save, name);

        Debug.Log($"Saved: {name}");
    }
    public void LoadSavedLevel(string fileName)
    {
        SavedLevel save = LevelSerializer.Load(fileName);

        if (save == null)
            return;

        ClearWorld();

        foreach (SavedVoxel voxel in save.voxels)
        {
            SetVoxel(
                new Vector3Int(voxel.x, voxel.y, voxel.z),
                voxel.type);
        }
    }

    public void ClearWorld()
    {
        foreach (var renderer in chunkRenderers.Values)
        {
            Destroy(renderer.gameObject);
        }

        chunks.Clear();
        chunkRenderers.Clear();
    }
    public void LoadGeneratedLevel(int seed, int worldSize)
    {
        ClearWorld();

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
        GameObject go = new($"Chunk {chunk.ChunkCoordinate}");

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

        Debug.Log($"World: {worldPos} -> Chunk: {chunkCoord}");

        if (!chunks.TryGetValue(chunkCoord, out Chunk chunk))
        {
            Debug.LogWarning($"No chunk exists at {chunkCoord}");
            return;
        }

        chunk.SetVoxel(
            localPos.x,
            localPos.y,
            localPos.z,
            new Voxel(type));

        chunkRenderers[chunkCoord].RebuildMesh();
    }
    private void OnGUI()
    {
        GUI.matrix = Matrix4x4.TRS(
            Vector3.zero,
            Quaternion.identity,
            Vector3.one * 2.5f
        );

        GUILayout.BeginArea(new Rect(10, 10, 220, 200));

        GUILayout.Label("Level Save/Load");

        fileName = GUILayout.TextField(fileName);

        GUILayout.Space(10);

        if (GUILayout.Button("Save"))
        {
            SaveLevel(fileName);
        }

        if (GUILayout.Button("Load"))
        {
            LoadSavedLevel(fileName);
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Generate Random"))
        {
            LoadGeneratedLevel(Random.Range(0, 99999), 1);
        }

        GUILayout.EndArea();
    }
}