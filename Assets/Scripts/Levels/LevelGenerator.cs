using System.Collections.Generic;
using UnityEngine;

public static class LevelGenerator
{
    // =====================================================
    // GENERATION ENTRY POINT
    // =====================================================

    public static LevelData Generate(
        int seed,
        int widthInChunks,
        int heightInChunks,
        int depthInChunks)
    {
        Random.InitState(seed);

        widthInChunks =
            Mathf.Max(1, widthInChunks);

        heightInChunks =
            Mathf.Max(1, heightInChunks);

        depthInChunks =
            Mathf.Max(1, depthInChunks);

        LevelData data =
            new LevelData
            {
                seed = seed,

                chunkSize =
                    Chunk.ChunkSize,

                widthInChunks =
                    widthInChunks,

                heightInChunks =
                    heightInChunks,

                depthInChunks =
                    depthInChunks,

                chunks =
                    new VoxelType[
                        widthInChunks,
                        heightInChunks,
                        depthInChunks][,,]
            };

        // 1. Solid base volume.
        LayoutPass(data);

        // 2. Authored procedural features.
        FeaturePass(data);

        // 3. Future rule verification / repairs.
        ValidationPass(data);

        // 4. Future material variation.
        MaterialPass(data);

        return data;
    }

    // =====================================================
    // LAYOUT
    // =====================================================

    private static void LayoutPass(
        LevelData data)
    {
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
                    VoxelType[,,] chunk =
                        CreateEmptyChunk();

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
                                chunk[x, y, z] =
                                    VoxelType.Granite;
                            }
                        }
                    }

                    data.chunks[
                        cx,
                        cy,
                        cz] =
                        chunk;
                }
            }
        }
    }

    // =====================================================
    // FEATURE PASS
    // =====================================================

    private static void FeaturePass(
        LevelData data)
    {
        HashSet<Vector3Int> reserved =
            new();

        GenerateWaterPool(
            data,
            reserved);

        GenerateLavaPool(
            data,
            reserved);

        GenerateHiddenCave(
            data,
            reserved);
    }

    // =====================================================
    // WATER POOL
    // =====================================================

    private static void GenerateWaterPool(
        LevelData data,
        HashSet<Vector3Int> reserved)
    {
        int worldWidth =
            GetWorldWidth(data);

        int worldHeight =
            GetWorldHeight(data);

        int worldDepth =
            GetWorldDepth(data);

        // Need enough room for an organic pool
        // plus its Vine border.
        if (
            worldWidth < 9 ||
            worldDepth < 9 ||
            worldHeight < 3)
        {
            Debug.LogWarning(
                "Level too small for water pool.");

            return;
        }

        const int maxAttempts = 50;

        for (
            int attempt = 0;
            attempt < maxAttempts;
            attempt++)
        {
            // At least 5 voxels across.
            int radiusX =
                Random.Range(3, 6);

            int radiusZ =
                Random.Range(2, 5);

            // Encourage an oblong shape rather than
            // a roughly circular one.
            if (radiusX == radiusZ)
            {
                if (radiusX < 5)
                    radiusX++;
                else
                    radiusZ--;
            }

            int depth =
                Random.Range(2, 5);

            int marginX =
                radiusX + 2;

            int marginZ =
                radiusZ + 2;

            if (
                worldWidth <= marginX * 2 ||
                worldDepth <= marginZ * 2 ||
                worldHeight <= depth)
            {
                continue;
            }

            int centerX =
                Random.Range(
                    marginX,
                    worldWidth - marginX);

            int centerZ =
                Random.Range(
                    marginZ,
                    worldDepth - marginZ);

            int topY =
                worldHeight - 1;

            HashSet<Vector2Int> footprint =
                CreateOrganicEllipseFootprint(
                    radiusX,
                    radiusZ);

            List<Vector3Int> waterVoxels =
                new();

            foreach (
                Vector2Int offset
                in footprint)
            {
                for (
                    int yOffset = 0;
                    yOffset < depth;
                    yOffset++)
                {
                    Vector3Int pos =
                        new(
                            centerX + offset.x,
                            topY - yOffset,
                            centerZ + offset.y);

                    waterVoxels.Add(
                        pos);
                }
            }

            // Reserve the water volume plus a one-voxel
            // horizontal border around it.
            if (
                ConflictsWithReserved(
                    waterVoxels,
                    reserved,
                    horizontalPadding: 1,
                    verticalPadding: 0))
            {
                continue;
            }

            foreach (
                Vector3Int pos
                in waterVoxels)
            {
                SetVoxel(
                    data,
                    pos,
                    VoxelType.Water);

                reserved.Add(
                    pos);
            }

            // -----------------------------------------
            // VINE BANK
            // -----------------------------------------
            //
            // "Surrounding" is interpreted as the
            // exposed ground immediately bordering the
            // surface of the pool.
            //
            // We do not create a subterranean Vine shell.

            HashSet<Vector3Int> vinePositions =
                new();

            foreach (
                Vector2Int waterOffset
                in footprint)
            {
                for (
                    int dx = -1;
                    dx <= 1;
                    dx++)
                {
                    for (
                        int dz = -1;
                        dz <= 1;
                        dz++)
                    {
                        if (
                            dx == 0 &&
                            dz == 0)
                        {
                            continue;
                        }

                        Vector2Int neighborOffset =
                            waterOffset +
                            new Vector2Int(
                                dx,
                                dz);

                        if (
                            footprint.Contains(
                                neighborOffset))
                        {
                            continue;
                        }

                        Vector3Int vinePos =
                            new(
                                centerX +
                                neighborOffset.x,

                                topY,

                                centerZ +
                                neighborOffset.y);

                        if (
                            IsInsideWorld(
                                data,
                                vinePos))
                        {
                            vinePositions.Add(
                                vinePos);
                        }
                    }
                }
            }

            foreach (
                Vector3Int vinePos
                in vinePositions)
            {
                // Don't overwrite water itself or
                // another reserved feature.
                if (
                    GetVoxel(
                        data,
                        vinePos) ==
                    VoxelType.Water)
                {
                    continue;
                }

                SetVoxel(
                    data,
                    vinePos,
                    VoxelType.Vine);

                reserved.Add(
                    vinePos);
            }

            Debug.Log(
                $"Generated Water Pool at " +
                $"({centerX}, {topY}, {centerZ}), " +
                $"depth {depth}.");

            return;
        }

        Debug.LogWarning(
            "Could not find valid placement for water pool.");
    }

    // =====================================================
    // LAVA POOL
    // =====================================================

    private static void GenerateLavaPool(
        LevelData data,
        HashSet<Vector3Int> reserved)
    {
        int worldWidth =
            GetWorldWidth(data);

        int worldHeight =
            GetWorldHeight(data);

        int worldDepth =
            GetWorldDepth(data);

        // Lava requires:
        // at least 5 Lava layers
        // + one Granite layer underneath.
        if (
            worldWidth < 7 ||
            worldDepth < 7 ||
            worldHeight < 6)
        {
            Debug.LogWarning(
                "Level too small for lava pool.");

            return;
        }

        const int maxAttempts = 50;

        for (
            int attempt = 0;
            attempt < maxAttempts;
            attempt++)
        {
            int sizeX =
                Random.Range(3, 7);

            int sizeZ =
                Random.Range(3, 7);

            int lavaDepth =
                Random.Range(5, 9);

            if (
                worldHeight <
                lavaDepth + 1)
            {
                lavaDepth =
                    worldHeight - 1;
            }

            int startX =
                Random.Range(
                    2,
                    Mathf.Max(
                        3,
                        worldWidth -
                        sizeX -
                        1));

            int startZ =
                Random.Range(
                    2,
                    Mathf.Max(
                        3,
                        worldDepth -
                        sizeZ -
                        1));

            if (
                startX + sizeX >= worldWidth ||
                startZ + sizeZ >= worldDepth)
            {
                continue;
            }

            int topY =
                worldHeight - 1;

            List<Vector3Int> featureVoxels =
                new();

            // Lava volume.
            for (
                int x = 0;
                x < sizeX;
                x++)
            {
                for (
                    int z = 0;
                    z < sizeZ;
                    z++)
                {
                    for (
                        int d = 0;
                        d < lavaDepth;
                        d++)
                    {
                        featureVoxels.Add(
                            new Vector3Int(
                                startX + x,
                                topY - d,
                                startZ + z));
                    }

                    // Explicit Granite floor underneath.
                    featureVoxels.Add(
                        new Vector3Int(
                            startX + x,
                            topY - lavaDepth,
                            startZ + z));
                }
            }

            if (
                ConflictsWithReserved(
                    featureVoxels,
                    reserved,
                    horizontalPadding: 2,
                    verticalPadding: 1))
            {
                continue;
            }

            for (
                int x = 0;
                x < sizeX;
                x++)
            {
                for (
                    int z = 0;
                    z < sizeZ;
                    z++)
                {
                    for (
                        int d = 0;
                        d < lavaDepth;
                        d++)
                    {
                        Vector3Int lavaPos =
                            new(
                                startX + x,
                                topY - d,
                                startZ + z);

                        SetVoxel(
                            data,
                            lavaPos,
                            VoxelType.Lava);

                        reserved.Add(
                            lavaPos);
                    }

                    Vector3Int floorPos =
                        new(
                            startX + x,
                            topY - lavaDepth,
                            startZ + z);

                    SetVoxel(
                        data,
                        floorPos,
                        VoxelType.Granite);

                    reserved.Add(
                        floorPos);
                }
            }

            Debug.Log(
                $"Generated Lava Pool at " +
                $"({startX}, {topY}, {startZ}), " +
                $"size {sizeX}x{sizeZ}, " +
                $"depth {lavaDepth}.");

            return;
        }

        Debug.LogWarning(
            "Could not find valid placement for lava pool.");
    }

    // =====================================================
    // HIDDEN CAVE
    // =====================================================

    private static void GenerateHiddenCave(
        LevelData data,
        HashSet<Vector3Int> reserved)
    {
        int worldWidth =
            GetWorldWidth(data);

        int worldHeight =
            GetWorldHeight(data);

        int worldDepth =
            GetWorldDepth(data);

        // Cave minimum:
        //
        // 7 wide
        // 7 deep
        // 5 high
        //
        // plus at least one solid layer
        // around the outside.
        if (
            worldWidth < 9 ||
            worldDepth < 9 ||
            worldHeight < 7)
        {
            Debug.LogWarning(
                "Level too small for hidden cave.");

            return;
        }

        const int maxAttempts = 75;

        for (
            int attempt = 0;
            attempt < maxAttempts;
            attempt++)
        {
            int caveWidth =
                Random.Range(7, 13);

            int caveDepth =
                Random.Range(7, 13);

            int caveHeight =
                Random.Range(5, 9);

            caveWidth =
                Mathf.Min(
                    caveWidth,
                    worldWidth - 2);

            caveDepth =
                Mathf.Min(
                    caveDepth,
                    worldDepth - 2);

            caveHeight =
                Mathf.Min(
                    caveHeight,
                    worldHeight - 2);

            // -----------------------------------------
            // CHOOSE A SIDE
            // -----------------------------------------
            //
            // The chamber is deliberately placed near
            // an outer wall, but remains hidden behind
            // 1-3 solid voxel layers.
            //
            // This makes it discoverable by Puzzle
            // layer scrolling without exposing it.

            PuzzleSide side =
                (PuzzleSide)Random.Range(
                    0,
                    4);

            int shellThickness =
                Random.Range(
                    1,
                    4);

            int startX;
            int startZ;

            switch (side)
            {
                case PuzzleSide.North:

                    startZ =
                        worldDepth -
                        shellThickness -
                        caveDepth;

                    startX =
                        Random.Range(
                            1,
                            worldWidth -
                            caveWidth);

                    break;

                case PuzzleSide.South:

                    startZ =
                        shellThickness;

                    startX =
                        Random.Range(
                            1,
                            worldWidth -
                            caveWidth);

                    break;

                case PuzzleSide.East:

                    startX =
                        worldWidth -
                        shellThickness -
                        caveWidth;

                    startZ =
                        Random.Range(
                            1,
                            worldDepth -
                            caveDepth);

                    break;

                case PuzzleSide.West:

                    startX =
                        shellThickness;

                    startZ =
                        Random.Range(
                            1,
                            worldDepth -
                            caveDepth);

                    break;

                default:

                    startX = 1;
                    startZ = 1;

                    break;
            }

            // Keep at least one voxel above and below.
            int startY =
                Random.Range(
                    1,
                    worldHeight -
                    caveHeight);

            if (
                startX < 1 ||
                startZ < 1 ||
                startY < 1)
            {
                continue;
            }

            if (
                startX + caveWidth >
                worldWidth - 1 ||
                startZ + caveDepth >
                worldDepth - 1 ||
                startY + caveHeight >
                worldHeight - 1)
            {
                continue;
            }

            List<Vector3Int> caveVoxels =
                new();

            // -----------------------------------------
            // ROUNDED / IRREGULAR CHAMBER
            // -----------------------------------------
            //
            // Start from a rounded rectangular volume.
            // Boundary noise prevents every cave from
            // looking like a clean cuboid.

            Vector3 caveCenter =
                new(
                    startX +
                    (caveWidth - 1) * 0.5f,

                    startY +
                    (caveHeight - 1) * 0.5f,

                    startZ +
                    (caveDepth - 1) * 0.5f);

            float radiusX =
                caveWidth * 0.5f;

            float radiusY =
                caveHeight * 0.5f;

            float radiusZ =
                caveDepth * 0.5f;

            for (
                int x = startX;
                x < startX + caveWidth;
                x++)
            {
                for (
                    int y = startY;
                    y < startY + caveHeight;
                    y++)
                {
                    for (
                        int z = startZ;
                        z < startZ + caveDepth;
                        z++)
                    {
                        float nx =
                            Mathf.Abs(
                                x - caveCenter.x) /
                            radiusX;

                        float ny =
                            Mathf.Abs(
                                y - caveCenter.y) /
                            radiusY;

                        float nz =
                            Mathf.Abs(
                                z - caveCenter.z) /
                            radiusZ;

                        // Squared ellipsoid distance.
                        float distance =
                            nx * nx +
                            ny * ny +
                            nz * nz;

                        // Slight random boundary wobble.
                        float threshold =
                            Random.Range(
                                0.85f,
                                1.20f);

                        if (
                            distance <=
                            threshold)
                        {
                            caveVoxels.Add(
                                new Vector3Int(
                                    x,
                                    y,
                                    z));
                        }
                    }
                }
            }

            // Ensure a substantial central core exists,
            // regardless of edge randomization.
            AddCaveCore(
                caveVoxels,
                startX,
                startY,
                startZ,
                caveWidth,
                caveHeight,
                caveDepth);

            if (
                ConflictsWithReserved(
                    caveVoxels,
                    reserved,
                    horizontalPadding: 2,
                    verticalPadding: 2))
            {
                continue;
            }

            foreach (
                Vector3Int cavePos
                in caveVoxels)
            {
                SetVoxel(
                    data,
                    cavePos,
                    VoxelType.Air);

                reserved.Add(
                    cavePos);
            }

            Debug.Log(
                $"Generated hidden cave near {side}, " +
                $"shell thickness {shellThickness}, " +
                $"nominal size " +
                $"{caveWidth}x{caveHeight}x{caveDepth}.");

            return;
        }

        Debug.LogWarning(
            "Could not find valid placement for hidden cave.");
    }

    // =====================================================
    // CAVE CORE
    // =====================================================

    private static void AddCaveCore(
        List<Vector3Int> caveVoxels,
        int startX,
        int startY,
        int startZ,
        int width,
        int height,
        int depth)
    {
        HashSet<Vector3Int> unique =
            new(
                caveVoxels);

        // Guarantee at least a 5x3x5 central open core.
        //
        // The full chamber remains at least nominally
        // 7x7 horizontally and 5 high, while the edges
        // retain their organic shape.

        int coreWidth =
            Mathf.Min(
                5,
                width);

        int coreDepth =
            Mathf.Min(
                5,
                depth);

        int coreHeight =
            Mathf.Min(
                3,
                height);

        int coreStartX =
            startX +
            (width - coreWidth) / 2;

        int coreStartZ =
            startZ +
            (depth - coreDepth) / 2;

        int coreStartY =
            startY +
            (height - coreHeight) / 2;

        for (
            int x = 0;
            x < coreWidth;
            x++)
        {
            for (
                int y = 0;
                y < coreHeight;
                y++)
            {
                for (
                    int z = 0;
                    z < coreDepth;
                    z++)
                {
                    unique.Add(
                        new Vector3Int(
                            coreStartX + x,
                            coreStartY + y,
                            coreStartZ + z));
                }
            }
        }

        caveVoxels.Clear();

        caveVoxels.AddRange(
            unique);
    }

    // =====================================================
    // ORGANIC WATER FOOTPRINT
    // =====================================================

    private static HashSet<Vector2Int>
        CreateOrganicEllipseFootprint(
            int radiusX,
            int radiusZ)
    {
        HashSet<Vector2Int> result =
            new();

        for (
            int x = -radiusX;
            x <= radiusX;
            x++)
        {
            for (
                int z = -radiusZ;
                z <= radiusZ;
                z++)
            {
                float nx =
                    (float)x /
                    radiusX;

                float nz =
                    (float)z /
                    radiusZ;

                float ellipseDistance =
                    nx * nx +
                    nz * nz;

                // Interior is stable.
                //
                // Only the outer region gets noticeable
                // random variation, producing an irregular
                // pond edge without fragmenting the shape.
                float threshold;

                if (
                    ellipseDistance <
                    0.60f)
                {
                    threshold =
                        1.0f;
                }
                else
                {
                    threshold =
                        Random.Range(
                            0.82f,
                            1.18f);
                }

                if (
                    ellipseDistance <=
                    threshold)
                {
                    result.Add(
                        new Vector2Int(
                            x,
                            z));
                }
            }
        }

        // Guarantee useful dimensions through the center.
        for (
            int x = -2;
            x <= 2;
            x++)
        {
            result.Add(
                new Vector2Int(
                    x,
                    0));
        }

        for (
            int z = -2;
            z <= 2;
            z++)
        {
            result.Add(
                new Vector2Int(
                    0,
                    z));
        }

        return result;
    }

    // =====================================================
    // FEATURE RESERVATION
    // =====================================================

    private static bool ConflictsWithReserved(
        IEnumerable<Vector3Int> positions,
        HashSet<Vector3Int> reserved,
        int horizontalPadding,
        int verticalPadding)
    {
        foreach (
            Vector3Int pos
            in positions)
        {
            for (
                int dx = -horizontalPadding;
                dx <= horizontalPadding;
                dx++)
            {
                for (
                    int dy = -verticalPadding;
                    dy <= verticalPadding;
                    dy++)
                {
                    for (
                        int dz = -horizontalPadding;
                        dz <= horizontalPadding;
                        dz++)
                    {
                        Vector3Int check =
                            pos +
                            new Vector3Int(
                                dx,
                                dy,
                                dz);

                        if (
                            reserved.Contains(
                                check))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    // =====================================================
    // VALIDATION
    // =====================================================

    private static void ValidationPass(
        LevelData data)
    {
        // Future:
        //
        // - ensure caves have usable dimensions
        // - ensure water has valid shoreline
        // - ensure lava Granite floor is intact
        // - ensure gameplay routes exist
        // - ensure SpawnPoints remain reachable
        // - repair invalid feature overlaps
    }

    // =====================================================
    // MATERIALS
    // =====================================================

    private static void MaterialPass(
        LevelData data)
    {
        // Future:
        //
        // Dirt distribution
        // Granite formations
        // Snow regions
        // additional environmental decoration
    }

    // =====================================================
    // WORLD VOXEL ACCESS
    // =====================================================

    private static void SetVoxel(
        LevelData data,
        Vector3Int worldPos,
        VoxelType type)
    {
        if (
            !IsInsideWorld(
                data,
                worldPos))
        {
            return;
        }

        int cx =
            worldPos.x /
            Chunk.ChunkSize;

        int cy =
            worldPos.y /
            Chunk.ChunkSize;

        int cz =
            worldPos.z /
            Chunk.ChunkSize;

        int lx =
            worldPos.x %
            Chunk.ChunkSize;

        int ly =
            worldPos.y %
            Chunk.ChunkSize;

        int lz =
            worldPos.z %
            Chunk.ChunkSize;

        data.chunks[
            cx,
            cy,
            cz]
        [
            lx,
            ly,
            lz
        ] =
            type;
    }

    private static VoxelType GetVoxel(
        LevelData data,
        Vector3Int worldPos)
    {
        if (
            !IsInsideWorld(
                data,
                worldPos))
        {
            return VoxelType.Air;
        }

        int cx =
            worldPos.x /
            Chunk.ChunkSize;

        int cy =
            worldPos.y /
            Chunk.ChunkSize;

        int cz =
            worldPos.z /
            Chunk.ChunkSize;

        int lx =
            worldPos.x %
            Chunk.ChunkSize;

        int ly =
            worldPos.y %
            Chunk.ChunkSize;

        int lz =
            worldPos.z %
            Chunk.ChunkSize;

        return
            data.chunks[
                cx,
                cy,
                cz]
            [
                lx,
                ly,
                lz
            ];
    }

    private static bool IsInsideWorld(
        LevelData data,
        Vector3Int pos)
    {
        return
            pos.x >= 0 &&
            pos.y >= 0 &&
            pos.z >= 0 &&

            pos.x <
            GetWorldWidth(data) &&

            pos.y <
            GetWorldHeight(data) &&

            pos.z <
            GetWorldDepth(data);
    }

    // =====================================================
    // WORLD DIMENSIONS
    // =====================================================

    private static int GetWorldWidth(
        LevelData data)
    {
        return
            data.widthInChunks *
            Chunk.ChunkSize;
    }

    private static int GetWorldHeight(
        LevelData data)
    {
        return
            data.heightInChunks *
            Chunk.ChunkSize;
    }

    private static int GetWorldDepth(
        LevelData data)
    {
        return
            data.depthInChunks *
            Chunk.ChunkSize;
    }

    // =====================================================
    // CHUNK CREATION
    // =====================================================

    private static VoxelType[,,]
        CreateEmptyChunk()
    {
        return new VoxelType[
            Chunk.ChunkSize,
            Chunk.ChunkSize,
            Chunk.ChunkSize];
    }
}