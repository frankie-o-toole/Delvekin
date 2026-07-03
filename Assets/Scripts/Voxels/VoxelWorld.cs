using System.Collections.Generic;
using UnityEngine;

public class VoxelWorld : MonoBehaviour
{
    [SerializeField] private OrbitCameraMode orbitCamera;

    private Dictionary<Vector3Int, Chunk> chunks = new();
    private Dictionary<Vector3Int, ChunkRenderer> chunkRenderers = new();

    public Material voxelMaterial;

    private LevelData currentLevel;
    private string fileName = "TestLevel";

    private void Start()
    {
        ChunkRefreshSystem.OnRefreshRequested += RebuildAllChunks;
        LoadGeneratedLevel(1234, 1);
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

        VoxelVisibilitySystem.SetView(SliceAxis.Z, +1);

        BuildFromSavedLevel(save);

        VoxelVisibilitySystem.ResetVisibility();
        ChunkRefreshSystem.RequestFullRefresh();
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

        VoxelVisibilitySystem.ResetVisibility();
        ChunkRefreshSystem.RequestFullRefresh();

        BuildFromLevel(currentLevel);
    }
    private void BuildFromSavedLevel(SavedLevel save)
    {
        ClearWorld();

        // 1. disable systems that react to visuals
        VoxelVisibilitySystem.SetToInitialPuzzleState();

        // 2. PURE DATA BUILD (NO renderers, NO GetOrCreateChunk)
        foreach (SavedVoxel voxel in save.voxels)
        {
            Vector3Int worldPos = new(voxel.x, voxel.y, voxel.z);

            Vector3Int chunkCoord = VoxelMath.WorldToChunkCoord(worldPos);
            Vector3Int localPos = VoxelMath.WorldToLocalVoxel(worldPos);

            if (!chunks.TryGetValue(chunkCoord, out Chunk chunk))
            {
                chunk = new Chunk(chunkCoord);
                chunks.Add(chunkCoord, chunk);
            }

            chunk.SetVoxel(localPos.x, localPos.y, localPos.z, new Voxel(voxel.type));
        }

        // 3. NOW create renderers AFTER ALL DATA EXISTS
        foreach (var kvp in chunks)
        {
            CreateChunkRenderer(kvp.Value);
        }

        // 4. visibility setup AFTER renderers exist
        VoxelVisibilitySystem.SetBounds(0, Chunk.ChunkSize * 10);
        VoxelVisibilitySystem.SetView(SliceAxis.Z, +1);
        VoxelVisibilitySystem.ResetVisibility();

        // 5. final mesh pass
        ChunkRefreshSystem.RequestFullRefresh();
    }
    private void BuildFromLevel(LevelData data)
    {
        ClearWorld();

        for (int x = 0; x < data.worldSizeInChunks; x++)
            for (int z = 0; z < data.worldSizeInChunks; z++)
            {
                Chunk chunk = new(new Vector3Int(x, 0, z));

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

        // 1. compute center AFTER chunks exist
        Vector3 levelCenter =
            LevelBoundsUtility.CalculateCenter(chunks.Keys, Chunk.ChunkSize);

        orbitCamera.SetOrbitCenter(levelCenter);

        int maxDepth = data.worldSizeInChunks * Chunk.ChunkSize;
        VoxelVisibilitySystem.SetBounds(0, maxDepth - 1);

        // 2. IMPORTANT: set view BEFORE refresh
        VoxelVisibilitySystem.SetView(SliceAxis.Z, +1);

        // 3. now safe
        VoxelVisibilitySystem.ResetVisibility();
        ChunkRefreshSystem.RequestFullRefresh();
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
    private void RebuildAllChunks()
    {
        foreach (var renderer in chunkRenderers.Values)
        {
            renderer.RebuildMesh();
        }
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

        Chunk chunk = GetOrCreateChunk(chunkCoord);

        chunk.SetVoxel(
            localPos.x,
            localPos.y,
            localPos.z,
            new Voxel(type));

        chunkRenderers[chunkCoord].RebuildMesh();
    }
    private Chunk GetOrCreateChunk(Vector3Int chunkCoord)
    {
        if (!chunks.TryGetValue(chunkCoord, out Chunk chunk))
        {
            chunk = new Chunk(chunkCoord);
            chunks.Add(chunkCoord, chunk);
            CreateChunkRenderer(chunk);
        }

        return chunk;
    }
    public IEnumerable<Vector3Int> GetChunkCoordinates()
    {
        return chunks.Keys;
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