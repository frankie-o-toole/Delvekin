using UnityEngine;

public static class LevelGenerator
{
    public static LevelData Generate(int seed, int worldSizeInChunks)
    {
        Random.InitState(seed);

        LevelData data = new LevelData
        {
            seed = seed,
            chunkSize = Chunk.ChunkSize,
            worldSizeInChunks = worldSizeInChunks,
            chunks = new VoxelType[worldSizeInChunks, 1, worldSizeInChunks][,,]
        };

        // STEP 1: Layout pass (rock base)
        LayoutPass(data);

        // STEP 2: Carving pass (dirt tunnels / air spaces)
        CarvingPass(data);

        // STEP 3: Rule validation + fixes
        ValidationPass(data);

        // STEP 4: Material assignment (dirt vs granite logic)
        MaterialPass(data);

        return data;
    }

    private static void LayoutPass(LevelData data)
    {
        for (int cx = 0; cx < data.worldSizeInChunks; cx++)
            for (int cz = 0; cz < data.worldSizeInChunks; cz++)
            {
                var chunk = CreateEmptyChunk();

                for (int x = 0; x < Chunk.ChunkSize; x++)
                    for (int z = 0; z < Chunk.ChunkSize; z++)
                    {
                        for (int y = 0; y < Chunk.ChunkSize; y++)
                        {
                            chunk[x, y, z] = VoxelType.Granite;
                        }
                    }

                data.chunks[cx, 0, cz] = chunk;
            }
    }

    private static void CarvingPass(LevelData data)
    {
        // Simple test: carve random tunnels
        for (int cx = 0; cx < data.worldSizeInChunks; cx++)
            for (int cz = 0; cz < data.worldSizeInChunks; cz++)
            {
                var chunk = data.chunks[cx, 0, cz];

                int tunnels = Random.Range(3, 8);

                for (int i = 0; i < tunnels; i++)
                {
                    int x = Random.Range(0, Chunk.ChunkSize);
                    int z = Random.Range(0, Chunk.ChunkSize);

                    for (int y = 1; y < Chunk.ChunkSize - 1; y++)
                    {
                        chunk[x, y, z] = VoxelType.Air;
                    }
                }
            }
    }

    private static void ValidationPass(LevelData data)
    {
        // placeholder for BFS / rule checks later
    }

    private static void MaterialPass(LevelData data)
    {
        // future: dirt vs granite logic refinement
    }

    private static VoxelType[,,] CreateEmptyChunk()
    {
        return new VoxelType[Chunk.ChunkSize, Chunk.ChunkSize, Chunk.ChunkSize];
    }
}