using System.Collections.Generic;
using UnityEngine;

public class VoxelWorld : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField]
    private CameraStateController cameraStateController;

    [Header("Rendering")]
    public Material voxelMaterial;

    [Header("Fluid Simulation")]
    [SerializeField]
    private bool simulateFluids = true;

    [Tooltip("Useful for fluid-only tests. Normally fluids begin with the dwarf simulation.")]
    [SerializeField]
    private bool simulateFluidsBeforeDwarves;

    [SerializeField]
    private FluidTuning waterFluid = new()
    {
        tickInterval = 0.15f,
        maximumDownwardTransfer = 8,
        maximumHorizontalTransfer = 4,
        minimumHorizontalDifference = 1,
        drainSearchDistance = 24
    };

    [SerializeField]
    private FluidTuning lavaFluid = new()
    {
        tickInterval = 0.8f,
        maximumDownwardTransfer = 4,
        maximumHorizontalTransfer = 1,
        minimumHorizontalDifference = 2,
        drainSearchDistance = 16
    };

    private readonly Dictionary<Vector3Int, Chunk> chunks =
        new();

    private readonly Dictionary<Vector3Int, ChunkRenderer> chunkRenderers =
        new();

    private readonly Dictionary<Vector3Int, FluidChunkRenderer>
        fluidChunkRenderers = new();

    private readonly HashSet<Vector3Int> dirtyFluidChunks = new();

    private readonly List<Vector3Int> spawnPoints =
        new();

    private readonly List<Vector3Int> exitPoints =
        new();

    private LevelData currentLevel;
    private WaterSystem waterSystem;
    private FluidSimulation fluidSimulation;
    private bool fluidSimulationStarted;

    public event System.Action<Vector3Int, Voxel, Voxel>
        VoxelChanged;

    private string fileName =
        "TestLevel";

    // =====================================================
    // GENERATOR UI VALUES
    // =====================================================

    private string generationWidth =
        "5";

    private string generationHeight =
        "4";

    private string generationDepth =
        "5";

    private void Awake()
    {
        waterFluid ??= new FluidTuning();
        lavaFluid ??= new FluidTuning
        {
            tickInterval = 0.8f,
            maximumDownwardTransfer = 4,
            maximumHorizontalTransfer = 1,
            minimumHorizontalDifference = 2,
            drainSearchDistance = 16
        };

        waterSystem = new WaterSystem(this);

        // Water now has its own deliberately static phase-one data model.
        // The legacy solver remains active for Lava only until Lava receives
        // its own design pass.
        fluidSimulation = new FluidSimulation(
            this,
            waterFluid,
            lavaFluid,
            simulateWater: false,
            simulateLava: true);

        if (cameraStateController == null)
        {
            cameraStateController =
                FindFirstObjectByType<CameraStateController>();
        }
    }

    private void Update()
    {
        if (simulateFluids &&
            (fluidSimulationStarted || simulateFluidsBeforeDwarves))
        {
            fluidSimulation?.Tick(Time.deltaTime);
        }
    }

    public void StartFluidSimulation()
    {
        fluidSimulationStarted = true;
    }

    public float GetFluidFill01(Vector3Int worldPosition)
    {
        VoxelType type = GetVoxel(worldPosition).Type;

        if (type == VoxelType.Water)
        {
            return waterSystem?.GetFill01(worldPosition) ?? 1f;
        }

        if (type == VoxelType.Lava)
        {
            return fluidSimulation?.GetAmount(worldPosition) /
                   (float)FluidSimulation.MaximumAmount ?? 1f;
        }

        return 0f;
    }

    public int GetFluidAmount(Vector3Int worldPosition)
    {
        VoxelType type = GetVoxel(worldPosition).Type;

        if (type == VoxelType.Water)
        {
            return waterSystem?.GetAmount(worldPosition) ?? 0;
        }

        return type == VoxelType.Lava
            ? fluidSimulation?.GetAmount(worldPosition) ?? 0
            : 0;
    }

    public Vector3Int GetFluidFlowDirection(
        Vector3Int worldPosition)
    {
        VoxelType type = GetVoxel(worldPosition).Type;

        if (type == VoxelType.Water)
        {
            return waterSystem?.GetPrimaryFlowDirection(worldPosition) ??
                   Vector3Int.zero;
        }

        return type == VoxelType.Lava
            ? fluidSimulation?.GetFlowDirection(worldPosition) ??
              Vector3Int.zero
            : Vector3Int.zero;
    }

    private void Start()
    {
        ChunkRefreshSystem.OnRefreshRequested +=
            RebuildAllChunks;

        ChunkRefreshSystem.OnSliceRefreshRequested +=
            RebuildSliceChunks;

        LoadGeneratedLevel(
            1234,
            5,
            4,
            5);
    }

    private void OnDestroy()
    {
        waterSystem?.Dispose();
        fluidSimulation?.Dispose();

        ChunkRefreshSystem.OnRefreshRequested -=
            RebuildAllChunks;

        ChunkRefreshSystem.OnSliceRefreshRequested -=
            RebuildSliceChunks;
    }

    // =====================================================
    // SAVE
    // =====================================================

    public SavedLevel CreateSaveData()
    {
        SavedLevel save =
            new();

        foreach (var pair in chunks)
        {
            Vector3Int chunkCoord =
                pair.Key;

            Chunk chunk =
                pair.Value;

            for (
                int x = 0;
                x < Chunk.ChunkSize;
                x++)
            {
                for (
                    int y = 0;
                    y < Chunk.ChunkSize;
                    y++)
                {
                    for (
                        int z = 0;
                        z < Chunk.ChunkSize;
                        z++)
                    {
                        Voxel voxel =
                            chunk.GetVoxel(
                                x,
                                y,
                                z);

                        if (
                            voxel.Type ==
                            VoxelType.Air)
                        {
                            continue;
                        }

                        save.voxels.Add(
                            new SavedVoxel
                            {
                                x =
                                    chunkCoord.x *
                                    Chunk.ChunkSize +
                                    x,

                                y =
                                    chunkCoord.y *
                                    Chunk.ChunkSize +
                                    y,

                                z =
                                    chunkCoord.z *
                                    Chunk.ChunkSize +
                                    z,

                                type =
                                    voxel.Type,

                                facing =
                                    voxel.Facing
                            });
                    }
                }
            }
        }

        return save;
    }

    public void SaveLevel(
        string name)
    {
        SavedLevel save =
            CreateSaveData();

        LevelSerializer.Save(
            save,
            name);

        Debug.Log(
            $"Saved: {name}");
    }

    public void LoadSavedLevel(
        string fileName)
    {
        SavedLevel save =
            LevelSerializer.Load(
                fileName);

        if (save == null)
            return;

        BuildFromSavedLevel(
            save);
    }

    // =====================================================
    // WORLD CLEARING
    // =====================================================

    public void ClearWorld()
    {
        foreach (
            ChunkRenderer renderer
            in chunkRenderers.Values)
        {
            if (renderer != null)
            {
                Destroy(
                    renderer.gameObject);
            }
        }

        spawnPoints.Clear();

        chunks.Clear();

        chunkRenderers.Clear();
        fluidChunkRenderers.Clear();
    }

    // =====================================================
    // GENERATION
    // =====================================================

    public void LoadGeneratedLevel(
        int seed,
        int widthInChunks,
        int heightInChunks,
        int depthInChunks)
    {
        ClearWorld();

        currentLevel =
            LevelGenerator.Generate(
                seed,
                widthInChunks,
                heightInChunks,
                depthInChunks);

        BuildFromLevel(
            currentLevel);
    }

    // =====================================================
    // SPAWN POINTS
    // =====================================================

    public void ScanSpawnPoints()
    {
        spawnPoints.Clear();
        exitPoints.Clear();

        foreach (var pair in chunks)
        {
            Vector3Int chunkCoord =
                pair.Key;

            Chunk chunk =
                pair.Value;

            for (
                int x = 0;
                x < Chunk.ChunkSize;
                x++)
            {
                for (
                    int y = 0;
                    y < Chunk.ChunkSize;
                    y++)
                {
                    for (
                        int z = 0;
                        z < Chunk.ChunkSize;
                        z++)
                    {
                        Voxel voxel =
                            chunk.GetVoxel(
                                x,
                                y,
                                z);

                        if (voxel.Type != VoxelType.SpawnPoint &&
                            voxel.Type != VoxelType.ExitPoint)
                        {
                            continue;
                        }

                        Vector3Int worldPos =
                            new(
                                chunkCoord.x *
                                Chunk.ChunkSize +
                                x,

                                chunkCoord.y *
                                Chunk.ChunkSize +
                                y,

                                chunkCoord.z *
                                Chunk.ChunkSize +
                                z);

                        if (voxel.Type == VoxelType.SpawnPoint)
                        {
                            spawnPoints.Add(worldPos);
                        }
                        else
                        {
                            exitPoints.Add(worldPos);
                        }
                    }
                }
            }
        }

        Debug.Log(
            $"Found {spawnPoints.Count} spawn point(s).");
    }

    // =====================================================
    // BUILD SAVED LEVEL
    // =====================================================

    private void BuildFromSavedLevel(
        SavedLevel save)
    {
        fluidSimulationStarted = false;
        ClearWorld();

        VoxelVisibilitySystem
            .SetToInitialPuzzleState();

        // -------------------------
        // DATA PASS
        // -------------------------

        foreach (
            SavedVoxel voxel
            in save.voxels)
        {
            Vector3Int worldPos =
                new(
                    voxel.x,
                    voxel.y,
                    voxel.z);

            Vector3Int chunkCoord =
                VoxelMath.WorldToChunkCoord(
                    worldPos);

            Vector3Int localPos =
                VoxelMath.WorldToLocalVoxel(
                    worldPos);

            if (
                !chunks.TryGetValue(
                    chunkCoord,
                    out Chunk chunk))
            {
                chunk =
                    new Chunk(
                        chunkCoord);

                chunks.Add(
                    chunkCoord,
                    chunk);
            }

            chunk.SetVoxel(
                localPos.x,
                localPos.y,
                localPos.z,
                new Voxel(
                    voxel.type,
                    voxel.facing));
        }

        // -------------------------
        // RENDERER PASS
        // -------------------------

        foreach (
            var pair
            in chunks)
        {
            CreateChunkRenderer(
                pair.Value);
        }

        RefreshWorldSpatialState(recenterCamera: true);

        VoxelVisibilitySystem.SetView(
            SliceAxis.Z,
            +1);

        VoxelVisibilitySystem
            .ResetVisibility();

        waterSystem?.ResetFromWorld();
        fluidSimulation?.ResetFromWorld();

        ChunkRefreshSystem
            .RequestFullRefresh();
    }

    // =====================================================
    // BUILD GENERATED LEVEL
    // =====================================================

    private void BuildFromLevel(
        LevelData data)
    {
        fluidSimulationStarted = false;
        ClearWorld();

        for (
            int cx = 0;
            cx < data.widthInChunks;
            cx++)
        {
            for (
                int cy = 0;
                cy < data.heightInChunks;
                cy++)
            {
                for (
                    int cz = 0;
                    cz < data.depthInChunks;
                    cz++)
                {
                    Vector3Int chunkCoord =
                        new(
                            cx,
                            cy,
                            cz);

                    Chunk chunk =
                        new(
                            chunkCoord);

                    for (
                        int lx = 0;
                        lx < Chunk.ChunkSize;
                        lx++)
                    {
                        for (
                            int ly = 0;
                            ly < Chunk.ChunkSize;
                            ly++)
                        {
                            for (
                                int lz = 0;
                                lz < Chunk.ChunkSize;
                                lz++)
                            {
                                VoxelType type =
                                    data.chunks[
                                        cx,
                                        cy,
                                        cz]
                                    [
                                        lx,
                                        ly,
                                        lz
                                    ];

                                chunk.SetVoxel(
                                    lx,
                                    ly,
                                    lz,
                                    new Voxel(
                                        type));
                            }
                        }
                    }

                    chunks[
                        chunkCoord] =
                        chunk;
                }
            }
        }

        // Build renderers only after every chunk exists. This prevents
        // not-yet-added neighbours from temporarily appearing as Air and
        // producing internal boundary faces in larger generated levels.
        foreach (Chunk chunk in chunks.Values)
        {
            CreateChunkRenderer(
                chunk);
        }

        RefreshWorldSpatialState(recenterCamera: true);

        VoxelVisibilitySystem.SetView(
            SliceAxis.Z,
            +1);

        VoxelVisibilitySystem
            .ResetVisibility();

        waterSystem?.ResetFromWorld();
        fluidSimulation?.ResetFromWorld();

        ChunkRefreshSystem
            .RequestFullRefresh();
    }

    // =====================================================
    // CHUNK RENDERING
    // =====================================================

    private void CreateChunkRenderer(
        Chunk chunk)
    {
        GameObject go =
            new(
                $"Chunk {chunk.ChunkCoordinate}");

        go.transform.position =
            new Vector3(
                chunk.ChunkCoordinate.x *
                Chunk.ChunkSize,

                chunk.ChunkCoordinate.y *
                Chunk.ChunkSize,

                chunk.ChunkCoordinate.z *
                Chunk.ChunkSize);

        ChunkRenderer renderer =
            go.AddComponent<ChunkRenderer>();

        MeshRenderer meshRenderer =
            go.GetComponent<MeshRenderer>();

        meshRenderer.material =
            voxelMaterial;

        renderer.Initialize(
            chunk,
            this);

        chunkRenderers.Add(
            chunk.ChunkCoordinate,
            renderer);

        GameObject fluidObject = new("Fluids");
        fluidObject.transform.SetParent(go.transform, false);

        FluidChunkRenderer fluidRenderer =
            fluidObject.AddComponent<FluidChunkRenderer>();

        fluidObject.GetComponent<MeshRenderer>().material =
            voxelMaterial;

        fluidRenderer.Initialize(chunk, this);

        fluidChunkRenderers.Add(
            chunk.ChunkCoordinate,
            fluidRenderer);
    }

    // =====================================================
    // WORLD SPATIAL STATE
    // =====================================================

    private void RefreshWorldSpatialState(
    bool recenterCamera)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        Vector3 levelCenter =
            LevelBoundsUtility.CalculateCenter(
                chunks.Keys,
                Chunk.ChunkSize);

        if (cameraStateController != null)
        {
            cameraStateController.SetLevelCenter(
                levelCenter,
                recenterCamera);
        }

        if (TryCalculateOccupiedHorizontalBounds(
                out int minX,
                out int maxX,
                out int minZ,
                out int maxZ))
        {
            VoxelVisibilitySystem.SetBounds(
                minX,
                maxX,
                minZ,
                maxZ);
        }
    }

    private bool
        TryCalculateOccupiedHorizontalBounds(
            out int minX,
            out int maxX,
            out int minZ,
            out int maxZ)
    {
        bool foundVoxel =
            false;

        minX = 0;
        maxX = 0;

        minZ = 0;
        maxZ = 0;

        foreach (
            var pair
            in chunks)
        {
            Vector3Int chunkCoord =
                pair.Key;

            Chunk chunk =
                pair.Value;

            for (
                int x = 0;
                x < Chunk.ChunkSize;
                x++)
            {
                for (
                    int y = 0;
                    y < Chunk.ChunkSize;
                    y++)
                {
                    for (
                        int z = 0;
                        z < Chunk.ChunkSize;
                        z++)
                    {
                        Voxel voxel =
                            chunk.GetVoxel(
                                x,
                                y,
                                z);

                        if (
                            voxel.Type ==
                            VoxelType.Air)
                        {
                            continue;
                        }

                        int worldX =
                            chunkCoord.x *
                            Chunk.ChunkSize +
                            x;

                        int worldZ =
                            chunkCoord.z *
                            Chunk.ChunkSize +
                            z;

                        if (!foundVoxel)
                        {
                            minX =
                                worldX;

                            maxX =
                                worldX;

                            minZ =
                                worldZ;

                            maxZ =
                                worldZ;

                            foundVoxel =
                                true;
                        }
                        else
                        {
                            minX =
                                Mathf.Min(
                                    minX,
                                    worldX);

                            maxX =
                                Mathf.Max(
                                    maxX,
                                    worldX);

                            minZ =
                                Mathf.Min(
                                    minZ,
                                    worldZ);

                            maxZ =
                                Mathf.Max(
                                    maxZ,
                                    worldZ);
                        }
                    }
                }
            }
        }

        return foundVoxel;
    }

    // =====================================================
    // REFRESH
    // =====================================================

    private void RebuildAllChunks()
    {
        foreach (
            ChunkRenderer renderer
            in chunkRenderers.Values)
        {
            renderer.RebuildMesh();
        }

        foreach (FluidChunkRenderer renderer in fluidChunkRenderers.Values)
        {
            renderer.RebuildMesh();
        }
    }

    private void RebuildChunkAndNeighbors(
        Vector3Int chunkCoord)
    {
        RebuildChunk(
            chunkCoord);

        RebuildChunk(
            chunkCoord +
            Vector3Int.right);

        RebuildChunk(
            chunkCoord +
            Vector3Int.left);

        RebuildChunk(
            chunkCoord +
            Vector3Int.up);

        RebuildChunk(
            chunkCoord +
            Vector3Int.down);

        RebuildChunk(
            chunkCoord +
            new Vector3Int(
                0,
                0,
                1));

        RebuildChunk(
            chunkCoord +
            new Vector3Int(
                0,
                0,
                -1));
    }

    private void RebuildChunk(
        Vector3Int chunkCoord)
    {
        if (
            chunkRenderers.TryGetValue(
                chunkCoord,
                out ChunkRenderer renderer))
        {
            renderer.RebuildMesh();
        }

        RebuildFluidChunk(chunkCoord);
    }

    private void RebuildFluidChunk(Vector3Int chunkCoord)
    {
        if (fluidChunkRenderers.TryGetValue(
                chunkCoord,
                out FluidChunkRenderer renderer))
        {
            renderer.RebuildMesh();
        }
    }

    // =====================================================
    // VOXEL ACCESS
    // =====================================================

    public Voxel GetVoxel(
        Vector3Int worldPos)
    {
        Vector3Int chunkCoord =
            VoxelMath.WorldToChunkCoord(
                worldPos);

        Vector3Int local =
            VoxelMath.WorldToLocalVoxel(
                worldPos);

        if (
            chunks.TryGetValue(
                chunkCoord,
                out Chunk chunk))
        {
            return chunk.GetVoxel(
                local.x,
                local.y,
                local.z);
        }

        return new Voxel(
            VoxelType.Air);
    }

    /// <summary>
    /// Returns true when this position belongs to a chunk that is
    /// already part of the loaded level. Jobs use this as the current
    /// temporary world-bound rule so they cannot expand the level by
    /// creating new chunks.
    /// </summary>
    public bool ContainsExistingChunkAt(
        Vector3Int worldPos)
    {
        Vector3Int chunkCoord =
            VoxelMath.WorldToChunkCoord(
                worldPos);

        return chunks.ContainsKey(
            chunkCoord);
    }

    public void SetVoxel(
        Vector3Int worldPos,
        VoxelType type)
    {
        SetVoxel(
            worldPos,
            type,
            PuzzleSide.North);
    }

    private void RebuildSliceChunks(
        SliceAxis axis,
        int oldVisibleBoundary,
        int newVisibleBoundary)
    {
        int oldChunkLayer =
            Mathf.FloorToInt(
                oldVisibleBoundary /
                (float)Chunk.ChunkSize);

        int newChunkLayer =
            Mathf.FloorToInt(
                newVisibleBoundary /
                (float)Chunk.ChunkSize);

        foreach (var pair in chunkRenderers)
        {
            int rendererLayer =
                axis == SliceAxis.X
                    ? pair.Key.x
                    : pair.Key.z;

            if (rendererLayer != oldChunkLayer &&
                rendererLayer != newChunkLayer)
            {
                continue;
            }

            pair.Value.RebuildMesh();
        }

        foreach (var pair in fluidChunkRenderers)
        {
            int rendererLayer =
                axis == SliceAxis.X
                    ? pair.Key.x
                    : pair.Key.z;

            if (rendererLayer != oldChunkLayer &&
                rendererLayer != newChunkLayer)
            {
                continue;
            }

            pair.Value.RebuildMesh();
        }
    }

    public void SetVoxel(
        Vector3Int worldPos,
        VoxelType type,
        PuzzleSide facing)
    {
        if (
            worldPos.x ==
                int.MinValue ||
            worldPos.y ==
                int.MinValue ||
            worldPos.z ==
                int.MinValue ||
            worldPos.x ==
                int.MaxValue ||
            worldPos.y ==
                int.MaxValue ||
            worldPos.z ==
                int.MaxValue)
        {
            Debug.LogWarning(
                $"Rejected invalid voxel edit position: {worldPos}");

            return;
        }

        Vector3Int chunkCoord =
            VoxelMath.WorldToChunkCoord(
                worldPos);

        Vector3Int localPos =
            VoxelMath.WorldToLocalVoxel(
                worldPos);

        Debug.Log(
            $"World: {worldPos} -> Chunk: {chunkCoord}");

        if (
            type ==
                VoxelType.Air &&
            !chunks.ContainsKey(
                chunkCoord))
        {
            return;
        }

        Chunk chunk =
            GetOrCreateChunk(
                chunkCoord);

        Voxel previousVoxel =
            chunk.GetVoxel(
                localPos.x,
                localPos.y,
                localPos.z);

        Voxel currentVoxel =
            new Voxel(
                type,
                facing);

        if (previousVoxel.Type == currentVoxel.Type &&
            previousVoxel.Facing == currentVoxel.Facing)
        {
            return;
        }

        chunk.SetVoxel(
            localPos.x,
            localPos.y,
            localPos.z,
            currentVoxel);

        VoxelChanged?.Invoke(
            worldPos,
            previousVoxel,
            currentVoxel);

        RefreshWorldSpatialState(recenterCamera: false);

        RebuildChunkAndNeighbors(
            chunkCoord);
    }

    public int SetVoxels(
    IEnumerable<Vector3Int> worldPositions,
    VoxelType type)
    {
        return SetVoxels(
            worldPositions,
            type,
            PuzzleSide.North);
    }

    public int SetVoxels(
    IEnumerable<Vector3Int> worldPositions,
    VoxelType type,
    PuzzleSide facing)
    {
        if (worldPositions == null)
        {
            return 0;
        }

        HashSet<Vector3Int> affectedChunks =
            new();

        int changedCount = 0;

        foreach (Vector3Int worldPos in worldPositions)
        {
            Vector3Int chunkCoord =
                VoxelMath.WorldToChunkCoord(
                    worldPos);

            Vector3Int localPos =
                VoxelMath.WorldToLocalVoxel(
                    worldPos);

            if (type == VoxelType.Air &&
                !chunks.ContainsKey(chunkCoord))
            {
                continue;
            }

            Chunk chunk =
                GetOrCreateChunk(
                    chunkCoord);

            Voxel existing =
                chunk.GetVoxel(
                    localPos.x,
                    localPos.y,
                    localPos.z);

            if (existing.Type == type &&
                (type != VoxelType.Ladder ||
                 existing.Facing == facing))
            {
                continue;
            }

            chunk.SetVoxel(
                localPos.x,
                localPos.y,
                localPos.z,
                new Voxel(
                    type,
                    facing));

            VoxelChanged?.Invoke(
                worldPos,
                existing,
                new Voxel(type, facing));

            affectedChunks.Add(
                chunkCoord);

            changedCount++;
        }

        if (changedCount == 0)
        {
            return 0;
        }

        RefreshWorldSpatialState(
            recenterCamera: false);

        foreach (Vector3Int chunkCoord in affectedChunks)
        {
            RebuildChunkAndNeighbors(
                chunkCoord);
        }

        return changedCount;
    }

    /// <summary>
    /// Applies mixed voxel states as one edit and rebuilds every affected
    /// chunk once. Fluid simulation and undo use this to avoid a rebuild per
    /// changed cell.
    /// </summary>
    public int SetVoxelStates(
        IReadOnlyDictionary<Vector3Int, Voxel> voxelStates)
    {
        if (voxelStates == null ||
            voxelStates.Count == 0)
        {
            return 0;
        }

        HashSet<Vector3Int> affectedChunks = new();
        int changedCount = 0;

        foreach (var pair in voxelStates)
        {
            Vector3Int worldPosition = pair.Key;
            Voxel currentVoxel = pair.Value;
            Vector3Int chunkCoordinate =
                VoxelMath.WorldToChunkCoord(worldPosition);

            if (currentVoxel.Type == VoxelType.Air &&
                !chunks.ContainsKey(chunkCoordinate))
            {
                continue;
            }

            Vector3Int localPosition =
                VoxelMath.WorldToLocalVoxel(worldPosition);

            Chunk chunk = GetOrCreateChunk(chunkCoordinate);
            Voxel previousVoxel = chunk.GetVoxel(
                localPosition.x,
                localPosition.y,
                localPosition.z);

            if (previousVoxel.Type == currentVoxel.Type &&
                previousVoxel.Facing == currentVoxel.Facing)
            {
                continue;
            }

            chunk.SetVoxel(
                localPosition.x,
                localPosition.y,
                localPosition.z,
                currentVoxel);

            affectedChunks.Add(chunkCoordinate);
            changedCount++;

            VoxelChanged?.Invoke(
                worldPosition,
                previousVoxel,
                currentVoxel);
        }

        if (changedCount == 0)
        {
            return 0;
        }

        RefreshWorldSpatialState(recenterCamera: false);

        HashSet<Vector3Int> chunksToRebuild = new();

        foreach (Vector3Int chunkCoordinate in affectedChunks)
        {
            chunksToRebuild.Add(chunkCoordinate);
            chunksToRebuild.Add(chunkCoordinate + Vector3Int.right);
            chunksToRebuild.Add(chunkCoordinate + Vector3Int.left);
            chunksToRebuild.Add(chunkCoordinate + Vector3Int.up);
            chunksToRebuild.Add(chunkCoordinate + Vector3Int.down);
            chunksToRebuild.Add(chunkCoordinate + Vector3Int.forward);
            chunksToRebuild.Add(chunkCoordinate + Vector3Int.back);
        }

        foreach (Vector3Int chunkCoordinate in chunksToRebuild)
        {
            RebuildChunk(chunkCoordinate);
        }

        return changedCount;
    }

    /// <summary>
    /// Applies simulation-owned fluid occupancy without recalculating world
    /// bounds, rebuilding terrain, or recooking terrain colliders.
    /// </summary>
    public int SetFluidVoxelStates(
        IReadOnlyDictionary<Vector3Int, Voxel> voxelStates)
    {
        if (voxelStates == null || voxelStates.Count == 0)
        {
            return 0;
        }

        int changedCount = 0;

        foreach (var pair in voxelStates)
        {
            Vector3Int worldPosition = pair.Key;
            Vector3Int chunkCoordinate =
                VoxelMath.WorldToChunkCoord(worldPosition);

            if (!chunks.TryGetValue(chunkCoordinate, out Chunk chunk))
            {
                continue;
            }

            Vector3Int localPosition =
                VoxelMath.WorldToLocalVoxel(worldPosition);

            Voxel previousVoxel = chunk.GetVoxel(
                localPosition.x,
                localPosition.y,
                localPosition.z);

            Voxel currentVoxel = pair.Value;

            if (previousVoxel.Type == currentVoxel.Type &&
                previousVoxel.Facing == currentVoxel.Facing)
            {
                continue;
            }

            chunk.SetVoxel(
                localPosition.x,
                localPosition.y,
                localPosition.z,
                currentVoxel);

            changedCount++;

            VoxelChanged?.Invoke(
                worldPosition,
                previousVoxel,
                currentVoxel);
        }

        return changedCount;
    }

    public void ForEachVoxel(
        System.Action<Vector3Int, Voxel> visitor)
    {
        if (visitor == null)
        {
            return;
        }

        foreach (var pair in chunks)
        {
            Vector3Int chunkOrigin =
                pair.Key * Chunk.ChunkSize;

            for (int x = 0; x < Chunk.ChunkSize; x++)
            {
                for (int y = 0; y < Chunk.ChunkSize; y++)
                {
                    for (int z = 0; z < Chunk.ChunkSize; z++)
                    {
                        Vector3Int localPosition = new(x, y, z);

                        visitor(
                            chunkOrigin + localPosition,
                            pair.Value.GetVoxel(x, y, z));
                    }
                }
            }
        }
    }

    public void RefreshVoxelVisuals(
        IEnumerable<Vector3Int> worldPositions)
    {
        if (worldPositions == null)
        {
            return;
        }

        dirtyFluidChunks.Clear();

        foreach (Vector3Int worldPosition in worldPositions)
        {
            Vector3Int chunkCoordinate =
                VoxelMath.WorldToChunkCoord(worldPosition);

            dirtyFluidChunks.Add(chunkCoordinate);

            Vector3Int localPosition =
                VoxelMath.WorldToLocalVoxel(worldPosition);

            if (localPosition.x == 0)
                dirtyFluidChunks.Add(chunkCoordinate + Vector3Int.left);
            else if (localPosition.x == Chunk.ChunkSize - 1)
                dirtyFluidChunks.Add(chunkCoordinate + Vector3Int.right);

            if (localPosition.y == 0)
                dirtyFluidChunks.Add(chunkCoordinate + Vector3Int.down);
            else if (localPosition.y == Chunk.ChunkSize - 1)
                dirtyFluidChunks.Add(chunkCoordinate + Vector3Int.up);

            if (localPosition.z == 0)
                dirtyFluidChunks.Add(chunkCoordinate + Vector3Int.back);
            else if (localPosition.z == Chunk.ChunkSize - 1)
                dirtyFluidChunks.Add(chunkCoordinate + Vector3Int.forward);
        }

        foreach (Vector3Int chunkCoordinate in dirtyFluidChunks)
        {
            RebuildFluidChunk(chunkCoordinate);
        }
    }

    private Chunk GetOrCreateChunk(
    Vector3Int chunkCoord)
    {
        if (!chunks.TryGetValue(
                chunkCoord,
                out Chunk chunk))
        {
            chunk =
                new Chunk(
                    chunkCoord);

            chunks.Add(
                chunkCoord,
                chunk);

            CreateChunkRenderer(
                chunk);
        }

        return chunk;
    }

    public IEnumerable<Vector3Int>
        GetChunkCoordinates()
    {
        return chunks.Keys;
    }

    // =====================================================
    // GENERATION UI HELPERS
    // =====================================================

    private int ParseChunkCount(
        string value)
    {
        if (
            !int.TryParse(
                value,
                out int result))
        {
            return 1;
        }

        return Mathf.Max(
            1,
            result);
    }

    // =====================================================
    // EDITOR UI
    // =====================================================

    private void OnGUI()
    {
        GUI.matrix =
            Matrix4x4.TRS(
                Vector3.zero,
                Quaternion.identity,
                Vector3.one * 2.5f);

        GUILayout.BeginArea(
            new Rect(
                10,
                10,
                260,
                330));

        GUILayout.Label(
            "Level Save/Load");

        fileName =
            GUILayout.TextField(
                fileName);

        GUILayout.Space(
            10);

        if (
            GUILayout.Button(
                "Save"))
        {
            SaveLevel(
                fileName);
        }

        if (
            GUILayout.Button(
                "Load"))
        {
            LoadSavedLevel(
                fileName);
        }

        GUILayout.Space(
            12);

        GUILayout.Label(
            "Generated Level Size");

        GUILayout.BeginHorizontal();

        GUILayout.Label(
            "Width",
            GUILayout.Width(70));

        generationWidth =
            GUILayout.TextField(
                generationWidth,
                GUILayout.Width(60));

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        GUILayout.Label(
            "Height",
            GUILayout.Width(70));

        generationHeight =
            GUILayout.TextField(
                generationHeight,
                GUILayout.Width(60));

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        GUILayout.Label(
            "Depth",
            GUILayout.Width(70));

        generationDepth =
            GUILayout.TextField(
                generationDepth,
                GUILayout.Width(60));

        GUILayout.EndHorizontal();

        GUILayout.Space(
            8);

        if (
            GUILayout.Button(
                "Generate Random"))
        {
            int width =
                ParseChunkCount(
                    generationWidth);

            int height =
                ParseChunkCount(
                    generationHeight);

            int depth =
                ParseChunkCount(
                    generationDepth);

            // Normalize displayed values too.
            generationWidth =
                width.ToString();

            generationHeight =
                height.ToString();

            generationDepth =
                depth.ToString();

            LoadGeneratedLevel(
                UnityEngine.Random.Range(
                    0,
                    99999),
                width,
                height,
                depth);
        }

        GUILayout.EndArea();
    }

    // =====================================================
    // DWARF-SAFE WRAPPERS
    // =====================================================

    public bool HasSupport(
        Vector3Int voxel)
    {
        return VoxelRules.IsSolid(
            GetVoxel(voxel));
    }

    public bool IsBlocked(
        Vector3Int worldPos)
    {
        Voxel voxel =
            GetVoxel(worldPos);

        // Ladder blocks ordinary horizontal movement even though it
        // deliberately does not provide ground support.
        return voxel.Type == VoxelType.Ladder ||
               VoxelRules.IsBlocked(voxel);
    }

    public bool IsLethal(
        Vector3Int worldPos)
    {
        VoxelType type =
            GetVoxel(
                worldPos).Type;

        return
            type ==
            VoxelType.Lava;
    }

    public bool IsFluid(
        Vector3Int worldPos)
    {
        VoxelType type =
            GetVoxel(
                worldPos).Type;

        return
            type ==
            VoxelType.Water;
    }

    public bool IsWalkable(
        Vector3Int worldPos)
    {
        if (
            IsBlocked(
                worldPos))
        {
            return false;
        }

        if (
            IsLethal(
                worldPos))
        {
            return false;
        }

        return true;
    }

    public Vector3Int GetSpawnPoint(
        int index = 0)
    {
        if (
            spawnPoints.Count == 0)
        {
            return Vector3Int.zero;
        }

        return
            spawnPoints[
                index %
                spawnPoints.Count];
    }

    public IReadOnlyList<Vector3Int>
        GetSpawnPoints()
    {
        return spawnPoints;
    }

    public IReadOnlyList<Vector3Int>
        GetExitPoints()
    {
        return exitPoints;
    }

    public bool TryGetVerticalBounds(
        out int minimumWorldY,
        out int maximumWorldY)
    {
        if (chunks.Count == 0)
        {
            minimumWorldY = 0;
            maximumWorldY = 0;
            return false;
        }

        int minimumChunkY = int.MaxValue;
        int maximumChunkY = int.MinValue;

        foreach (Vector3Int chunkCoordinate in chunks.Keys)
        {
            minimumChunkY = Mathf.Min(
                minimumChunkY,
                chunkCoordinate.y);

            maximumChunkY = Mathf.Max(
                maximumChunkY,
                chunkCoordinate.y);
        }

        minimumWorldY =
            minimumChunkY * Chunk.ChunkSize;

        maximumWorldY =
            (maximumChunkY + 1) * Chunk.ChunkSize - 1;

        return true;
    }
}
