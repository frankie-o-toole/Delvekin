using UnityEngine;

public static class LevelGenerator
{
    private const int BoundaryThickness = 1;
    private const int BaseFloorHeight = 9;
    private const int MainCeilingHeight = 38;

    public static LevelData Generate(
        int seed,
        int widthInChunks,
        int heightInChunks,
        int depthInChunks)
    {
        widthInChunks = Mathf.Max(1, widthInChunks);
        heightInChunks = Mathf.Max(1, heightInChunks);
        depthInChunks = Mathf.Max(1, depthInChunks);

        LevelData data =
            new()
            {
                seed = seed,
                chunkSize = Chunk.ChunkSize,
                widthInChunks = widthInChunks,
                heightInChunks = heightInChunks,
                depthInChunks = depthInChunks,
                chunks = new VoxelType[
                    widthInChunks,
                    heightInChunks,
                    depthInChunks][,,]
            };

        CreateChunks(data);
        FillTerrain(data);
        CarveMainCave(data);
        AddGameplayFormations(data);
        AddRooftopEntry(data);

        Debug.Log(
            $"Generated connected cave {GetWorldWidth(data)}x"
            + $"{GetWorldHeight(data)}x{GetWorldDepth(data)} "
            + $"from seed {seed}. No fluids were generated.");

        return data;
    }

    private static void CreateChunks(LevelData data)
    {
        for (int cx = 0; cx < data.widthInChunks; cx++)
        {
            for (int cy = 0; cy < data.heightInChunks; cy++)
            {
                for (int cz = 0; cz < data.depthInChunks; cz++)
                {
                    data.chunks[cx, cy, cz] =
                        new VoxelType[
                            Chunk.ChunkSize,
                            Chunk.ChunkSize,
                            Chunk.ChunkSize];
                }
            }
        }
    }

    private static void FillTerrain(LevelData data)
    {
        int worldWidth = GetWorldWidth(data);
        int worldHeight = GetWorldHeight(data);
        int worldDepth = GetWorldDepth(data);

        for (int x = 0; x < worldWidth; x++)
        {
            for (int y = 0; y < worldHeight; y++)
            {
                for (int z = 0; z < worldDepth; z++)
                {
                    bool openTop =
                        y >= worldHeight - 2;

                    VoxelType type = openTop
                        ? VoxelType.Air
                        : IsBoundaryVoxel(
                                x,
                                y,
                                z,
                                worldWidth,
                                worldHeight,
                                worldDepth)
                            ? VoxelType.Granite
                            : VoxelType.Dirt;

                    SetVoxel(
                        data,
                        new Vector3Int(x, y, z),
                        type);
                }
            }
        }
    }

    private static void CarveMainCave(LevelData data)
    {
        int worldWidth = GetWorldWidth(data);
        int worldHeight = GetWorldHeight(data);
        int worldDepth = GetWorldDepth(data);

        for (int x = 0; x < worldWidth; x++)
        {
            for (int z = 0; z < worldDepth; z++)
            {
                if (!IsInsideCaveFootprint(data, x, z))
                {
                    continue;
                }

                int floorHeight =
                    GetFloorHeight(data, x, z);

                for (int y = floorHeight + 1;
                     y < worldHeight;
                     y++)
                {
                    SetVoxel(
                        data,
                        new Vector3Int(x, y, z),
                        VoxelType.Air);
                }
            }
        }
    }

    private static bool IsInsideCaveFootprint(
        LevelData data,
        int x,
        int z)
    {
        int worldWidth = GetWorldWidth(data);
        int worldDepth = GetWorldDepth(data);

        float centreX = (worldWidth - 1) * 0.5f;
        float centreZ = (worldDepth - 1) * 0.5f;

        float radiusX =
            Mathf.Max(2f, centreX - 3f);

        float radiusZ =
            Mathf.Max(2f, centreZ - 3f);

        float normalizedX =
            Mathf.Abs(x - centreX) / radiusX;

        float normalizedZ =
            Mathf.Abs(z - centreZ) / radiusZ;

        // A fourth-power superellipse creates a large, broadly rectangular
        // cavern while retaining rounded, irregular outer walls.
        float shape =
            Mathf.Pow(normalizedX, 4f) +
            Mathf.Pow(normalizedZ, 4f);

        float seedOffset =
            data.seed * 0.0137f;

        float edgeNoise =
            Mathf.PerlinNoise(
                x * 0.09f + seedOffset,
                z * 0.09f + seedOffset * 0.61f);

        float threshold =
            1f + (edgeNoise - 0.5f) * 0.10f;

        return shape <= threshold;
    }

    private static int GetFloorHeight(
        LevelData data,
        int x,
        int z)
    {
        int width = GetWorldWidth(data);
        int depth = GetWorldDepth(data);

        int baseFloor =
            Mathf.Clamp(
                BaseFloorHeight,
                2,
                GetWorldHeight(data) - 9);

        int desiredFloor =
            baseFloor;

        // A six-voxel central plateau for Stair, Ladder and Digger tests.
        if (IsInsideRectangle(
                x,
                z,
                Mathf.RoundToInt(width * 0.38f),
                Mathf.RoundToInt(width * 0.68f),
                Mathf.RoundToInt(depth * 0.17f),
                Mathf.RoundToInt(depth * 0.49f)))
        {
            desiredFloor = baseFloor + 6;
        }
        else if (IsInsideRectangle(
                x,
                z,
                Mathf.RoundToInt(width * 0.62f),
                Mathf.RoundToInt(width * 0.88f),
                Mathf.RoundToInt(depth * 0.62f),
                Mathf.RoundToInt(depth * 0.86f)))
        {
            desiredFloor = baseFloor + 4;
        }
        else if (IsInsideRectangle(
                x,
                z,
                Mathf.RoundToInt(width * 0.11f),
                Mathf.RoundToInt(width * 0.34f),
                Mathf.RoundToInt(depth * 0.58f),
                Mathf.RoundToInt(depth * 0.84f)))
        {
            desiredFloor = baseFloor - 3;
        }

        return Mathf.Clamp(
            desiredFloor,
            2,
            GetWorldHeight(data) - 9);
    }

    private static int GetCeilingHeight(
        LevelData data,
        int x,
        int z)
    {
        int maximumCeiling =
            GetWorldHeight(data) -
            BoundaryThickness - 2;

        float seedOffset =
            data.seed * 0.0091f;

        float noise =
            Mathf.PerlinNoise(
                x * 0.055f + seedOffset,
                z * 0.055f + seedOffset * 1.37f);

        int variation =
            Mathf.RoundToInt(
                Mathf.Lerp(-2f, 2f, noise));

        int minimumCeiling =
            GetFloorHeight(data, x, z) + 7;

        return Mathf.Clamp(
            MainCeilingHeight + variation,
            minimumCeiling,
            maximumCeiling);
    }

    private static void AddGameplayFormations(LevelData data)
    {
        int width = GetWorldWidth(data);
        int depth = GetWorldDepth(data);

        AddFormation(
            data,
            Mathf.RoundToInt(width * 0.18f),
            Mathf.RoundToInt(width * 0.29f),
            Mathf.RoundToInt(depth * 0.23f),
            Mathf.RoundToInt(depth * 0.39f),
            12,
            VoxelType.Dirt);

        AddFormation(
            data,
            Mathf.RoundToInt(width * 0.72f),
            Mathf.RoundToInt(width * 0.82f),
            Mathf.RoundToInt(depth * 0.29f),
            Mathf.RoundToInt(depth * 0.46f),
            15,
            VoxelType.Dirt);

        // One limited Granite landmark provides protected-material feedback
        // without making most generated terrain unusable by jobs.
        AddFormation(
            data,
            Mathf.RoundToInt(width * 0.79f),
            Mathf.RoundToInt(width * 0.87f),
            Mathf.RoundToInt(depth * 0.70f),
            Mathf.RoundToInt(depth * 0.79f),
            8,
            VoxelType.Granite);
    }

    private static void AddFormation(
        LevelData data,
        int minimumX,
        int maximumX,
        int minimumZ,
        int maximumZ,
        int height,
        VoxelType type)
    {
        for (int x = minimumX; x <= maximumX; x++)
        {
            for (int z = minimumZ; z <= maximumZ; z++)
            {
                if (!IsInsideCaveFootprint(data, x, z))
                {
                    continue;
                }

                int floorHeight = GetFloorHeight(data, x, z);
                int ceilingHeight = GetCeilingHeight(data, x, z);

                int top =
                    Mathf.Min(
                        floorHeight + height,
                        ceilingHeight);

                for (int y = floorHeight + 1; y <= top; y++)
                {
                    SetVoxel(
                        data,
                        new Vector3Int(x, y, z),
                        type);
                }
            }
        }
    }

    private static void AddRooftopEntry(LevelData data)
    {
        int width = GetWorldWidth(data);
        int height = GetWorldHeight(data);
        int depth = GetWorldDepth(data);

        // Small custom generations do not have enough room for the elevated
        // entrance route. Keep them usable with an interior spawn instead.
        if (height < 40 || width < 15 || depth < 56)
        {
            AddInteriorSpawnPoint(data);
            return;
        }

        int centreX = width / 2;
        int spawnZ = Mathf.Clamp(depth / 8, 5, depth - 18);
        int holeCentreZ = Mathf.Min(spawnZ + 12, depth - 8);
        int spawnY = height - 1;
        // The cave itself has no ceiling. This elevated deck preserves the
        // opening sequence without covering or visually enclosing the level.
        for (int x = centreX - 8; x <= centreX + 8; x++)
        {
            for (int z = spawnZ - 3; z <= holeCentreZ + 7; z++)
            {
                SetVoxel(data, new Vector3Int(x, spawnY, z), VoxelType.Air);
                SetVoxel(data, new Vector3Int(x, spawnY - 1, z), VoxelType.Dirt);
            }
        }

        // An 11x11 opening comfortably clears the dwarf's 3x3 footprint.
        for (int x = centreX - 5; x <= centreX + 5; x++)
        {
            for (int z = holeCentreZ - 5; z <= holeCentreZ + 5; z++)
            {
                SetVoxel(
                    data,
                    new Vector3Int(x, spawnY - 1, z),
                    VoxelType.Air);
            }
        }

        int generatedStepCount = AddGiantEntrySteps(
            data,
            centreX,
            holeCentreZ,
            spawnY);

        Vector3Int spawnAnchor = new(centreX, spawnY, spawnZ);

        SetVoxel(
            data,
            spawnAnchor,
            VoxelType.SpawnPoint);

        Debug.Log(
            $"Generated elevated SpawnPoint at {spawnAnchor}, facing "
            + $"{generatedStepCount} descending Dirt platforms "
            + "with eight-voxel drops.");
    }

    private static int AddGiantEntrySteps(
        LevelData data,
        int centreX,
        int holeCentreZ,
        int spawnAnchorY)
    {
        const int StepWidth = 11;
        const int FirstStepDepth = 11;
        const int StepDrop = 8;
        int stepCount = Mathf.Max(
            1,
            Mathf.CeilToInt(
                (spawnAnchorY -
                 (BaseFloorHeight + 1) -
                 StepDrop) /
                (float)StepDrop));

        int halfWidth = StepWidth / 2;
        int stepStartZ = holeCentreZ - FirstStepDepth / 2;
        int previousAnchorY = spawnAnchorY;

        int remainingDepth =
            GetWorldDepth(data) -
            stepStartZ -
            FirstStepDepth -
            3;

        int followingStepDepth =
            stepCount > 1
                ? Mathf.Clamp(
                    remainingDepth / (stepCount - 1),
                    5,
                    9)
                : FirstStepDepth;

        for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
        {
            int stepDepth =
                stepIndex == 0
                    ? FirstStepDepth
                    : followingStepDepth;

            int stepTopY =
                spawnAnchorY -
                ((stepIndex + 1) * StepDrop) -
                1;

            int stepEndZ = stepStartZ + stepDepth - 1;

            for (int x = centreX - halfWidth;
                 x <= centreX + halfWidth;
                 x++)
            {
                for (int z = stepStartZ; z <= stepEndZ; z++)
                {
                    int localFloor = GetFloorHeight(data, x, z);

                    // These are true pillars rather than floating platforms.
                    for (int y = localFloor + 1; y <= stepTopY; y++)
                    {
                        SetVoxel(
                            data,
                            new Vector3Int(x, y, z),
                            VoxelType.Dirt);
                    }

                    // Clear both the dwarf's standing volume and the vertical
                    // fall path from the preceding, higher platform.
                    for (int y = stepTopY + 1;
                         y <= previousAnchorY + 4;
                         y++)
                    {
                        SetVoxel(
                            data,
                            new Vector3Int(x, y, z),
                            VoxelType.Air);
                    }
                }
            }

            previousAnchorY = stepTopY + 1;
            stepStartZ = stepEndZ + 1;
        }

        return stepCount;
    }

    private static void AddInteriorSpawnPoint(LevelData data)
    {
        int x = Mathf.Clamp(GetWorldWidth(data) / 2, 4, GetWorldWidth(data) - 5);
        int z = Mathf.Clamp(GetWorldDepth(data) / 4, 4, GetWorldDepth(data) - 5);
        int floorHeight = GetFloorHeight(data, x, z);
        Vector3Int spawnAnchor = new(x, floorHeight + 1, z);

        foreach (Vector3Int occupiedVoxel
                 in DwarfSpatialRules.GetOccupiedVoxels(spawnAnchor))
        {
            SetVoxel(data, occupiedVoxel, VoxelType.Air);
        }

        SetVoxel(data, spawnAnchor, VoxelType.SpawnPoint);

        Debug.Log(
            $"Generated interior SpawnPoint at {spawnAnchor}; "
            + "the level is too small for the rooftop entry.");
    }

    private static bool IsBoundaryVoxel(
        int x,
        int y,
        int z,
        int worldWidth,
        int worldHeight,
        int worldDepth)
    {
        return
            x < BoundaryThickness ||
            y < BoundaryThickness ||
            z < BoundaryThickness ||
            x >= worldWidth - BoundaryThickness ||
            y >= worldHeight - BoundaryThickness ||
            z >= worldDepth - BoundaryThickness;
    }

    private static bool IsInsideRectangle(
        int x,
        int z,
        int minimumX,
        int maximumX,
        int minimumZ,
        int maximumZ)
    {
        return
            x >= minimumX &&
            x <= maximumX &&
            z >= minimumZ &&
            z <= maximumZ;
    }

    private static void SetVoxel(
        LevelData data,
        Vector3Int worldPosition,
        VoxelType type)
    {
        if (!IsInsideWorld(data, worldPosition))
        {
            return;
        }

        int chunkX = worldPosition.x / Chunk.ChunkSize;
        int chunkY = worldPosition.y / Chunk.ChunkSize;
        int chunkZ = worldPosition.z / Chunk.ChunkSize;

        int localX = worldPosition.x % Chunk.ChunkSize;
        int localY = worldPosition.y % Chunk.ChunkSize;
        int localZ = worldPosition.z % Chunk.ChunkSize;

        data.chunks[chunkX, chunkY, chunkZ]
            [localX, localY, localZ] = type;
    }

    private static bool IsInsideWorld(
        LevelData data,
        Vector3Int position)
    {
        return
            position.x >= 0 &&
            position.y >= 0 &&
            position.z >= 0 &&
            position.x < GetWorldWidth(data) &&
            position.y < GetWorldHeight(data) &&
            position.z < GetWorldDepth(data);
    }

    private static int GetWorldWidth(LevelData data)
    {
        return data.widthInChunks * Chunk.ChunkSize;
    }

    private static int GetWorldHeight(LevelData data)
    {
        return data.heightInChunks * Chunk.ChunkSize;
    }

    private static int GetWorldDepth(LevelData data)
    {
        return data.depthInChunks * Chunk.ChunkSize;
    }
}
