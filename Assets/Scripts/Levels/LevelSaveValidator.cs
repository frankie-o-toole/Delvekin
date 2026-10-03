using System.Collections.Generic;
using UnityEngine;

public static class LevelSaveValidator
{
    public static bool Validate(
        LevelSaveData data,
        ICollection<string> errors)
    {
        if (data == null)
        {
            errors?.Add("Level data is null.");
            return false;
        }

        bool valid = true;

        if (data.schemaVersion <= 0 ||
            data.schemaVersion > LevelDefinition.CurrentSchemaVersion)
        {
            AddError(
                errors,
                $"Unsupported schema version {data.schemaVersion}.");
            valid = false;
        }

        if (data.chunkSize != Chunk.ChunkSize)
        {
            AddError(
                errors,
                $"Chunk size {data.chunkSize} does not match " +
                $"{Chunk.ChunkSize}.");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(data.levelId))
        {
            AddError(errors, "Level ID must not be empty.");
            valid = false;
        }

        if (data.totalDwarves < 1)
        {
            AddError(errors, "Total dwarves must be at least one.");
            valid = false;
        }

        if (data.requiredRescues < 1 ||
            data.requiredRescues > data.totalDwarves)
        {
            AddError(
                errors,
                "Required rescues must be between one and total dwarves.");
            valid = false;
        }

        HashSet<DwarfJobType> jobTypes = new();

        if (data.jobRules != null)
        {
            foreach (LevelJobRule rule in data.jobRules)
            {
                if (rule == null ||
                    rule.jobType == DwarfJobType.None ||
                    !jobTypes.Add(rule.jobType))
                {
                    AddError(
                        errors,
                        "Job rules must have unique, non-None job types.");
                    valid = false;
                    continue;
                }

                if (rule.defaultCount < 0 ||
                    rule.maximumCount < rule.defaultCount)
                {
                    AddError(
                        errors,
                        $"Job rule {rule.jobType} has invalid stock limits.");
                    valid = false;
                }
            }
        }

        if (data.sizeInChunks.x <= 0 ||
            data.sizeInChunks.y <= 0 ||
            data.sizeInChunks.z <= 0)
        {
            AddError(errors, "World size must be positive on every axis.");
            valid = false;
        }

        Vector3Int worldMinimum =
            data.originInChunks * Chunk.ChunkSize;
        Vector3Int worldMaximumExclusive =
            worldMinimum + data.sizeInChunks * Chunk.ChunkSize;
        Vector3Int gameplayMaximumExclusive =
            data.gameplayBoundsMinimum + data.gameplayBoundsSize;

        if (!ContainsBounds(
                worldMinimum,
                worldMaximumExclusive,
                data.gameplayBoundsMinimum,
                gameplayMaximumExclusive))
        {
            AddError(
                errors,
                "Gameplay bounds must be positive and inside world bounds.");
            valid = false;
        }

        HashSet<Vector3Int> voxelPositions = new();

        if (data.voxels != null)
        {
            foreach (LevelVoxelRecord voxel in data.voxels)
            {
                if (!Contains(
                        voxel.Position,
                        worldMinimum,
                        worldMaximumExclusive))
                {
                    AddError(
                        errors,
                        $"Voxel {voxel.Position} is outside world bounds.");
                    valid = false;
                }

                if (VoxelTraits.Has(voxel.Type, VoxelTrait.Empty))
                {
                    AddError(
                        errors,
                        $"Sparse voxel record {voxel.Position} is empty.");
                    valid = false;
                }

                if (!voxelPositions.Add(voxel.Position))
                {
                    AddError(
                        errors,
                        $"Duplicate voxel record at {voxel.Position}.");
                    valid = false;
                }
            }
        }

        HashSet<string> entityIds = new();

        if (data.entities == null)
        {
            return valid;
        }

        foreach (LevelEntitySaveRecord entity in data.entities)
        {
            if (entity == null)
            {
                AddError(errors, "Level contains a null entity record.");
                valid = false;
                continue;
            }

            if (string.IsNullOrWhiteSpace(entity.entityId) ||
                !entityIds.Add(entity.entityId))
            {
                AddError(
                    errors,
                    "Every entity must have a unique, non-empty ID.");
                valid = false;
            }

            Vector3Int size = entity.volumeSize;

            if (size.x <= 0 || size.y <= 0 || size.z <= 0)
            {
                AddError(
                    errors,
                    $"Entity '{entity.entityId}' has an invalid volume.");
                valid = false;
                continue;
            }

            if (entity.type == LevelEntityType.SpawnHouse)
            {
                if (entity.hasVisualPrefab &&
                    string.IsNullOrWhiteSpace(
                        entity.visualPrefabResourcePath))
                {
                    AddError(
                        errors,
                        $"Spawn House '{entity.entityId}' uses a visual " +
                        "prefab outside a Resources folder. Portable saves " +
                        "require a Resources-relative prefab path.");
                    valid = false;
                }

                Vector3Int spawnVoxel = Vector3Int.FloorToInt(
                    entity.position +
                    entity.rotation * entity.spawnMarkerLocalPosition);

                if (!Contains(
                        spawnVoxel,
                        data.gameplayBoundsMinimum,
                        gameplayMaximumExclusive))
                {
                    AddError(
                        errors,
                        $"Spawn House '{entity.entityId}' spawns outside " +
                        "gameplay bounds.");
                    valid = false;
                }

                continue;
            }

            Vector3Int entityMinimum = Vector3Int.FloorToInt(
                entity.position - (Vector3)size * 0.5f +
                Vector3.one * 0.001f);

            if (!ContainsBounds(
                    worldMinimum,
                    worldMaximumExclusive,
                    entityMinimum,
                    entityMinimum + size))
            {
                AddError(
                    errors,
                    $"Entity '{entity.entityId}' is outside world bounds.");
                valid = false;
            }
        }

        return valid;
    }

    private static bool Contains(
        Vector3Int position,
        Vector3Int minimum,
        Vector3Int maximumExclusive)
    {
        return
            position.x >= minimum.x &&
            position.y >= minimum.y &&
            position.z >= minimum.z &&
            position.x < maximumExclusive.x &&
            position.y < maximumExclusive.y &&
            position.z < maximumExclusive.z;
    }

    private static bool ContainsBounds(
        Vector3Int outerMinimum,
        Vector3Int outerMaximumExclusive,
        Vector3Int innerMinimum,
        Vector3Int innerMaximumExclusive)
    {
        return
            innerMaximumExclusive.x > innerMinimum.x &&
            innerMaximumExclusive.y > innerMinimum.y &&
            innerMaximumExclusive.z > innerMinimum.z &&
            innerMinimum.x >= outerMinimum.x &&
            innerMinimum.y >= outerMinimum.y &&
            innerMinimum.z >= outerMinimum.z &&
            innerMaximumExclusive.x <= outerMaximumExclusive.x &&
            innerMaximumExclusive.y <= outerMaximumExclusive.y &&
            innerMaximumExclusive.z <= outerMaximumExclusive.z;
    }

    private static void AddError(
        ICollection<string> errors,
        string message)
    {
        errors?.Add(message);
    }
}
