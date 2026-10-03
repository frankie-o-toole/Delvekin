using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Portable, versioned representation of one complete level. This is the
/// JSON boundary; LevelDefinition remains the authoritative authoring model.
/// </summary>
[Serializable]
public sealed class LevelSaveData
{
    public int schemaVersion;
    public int chunkSize;
    public string levelId;
    public string displayName;
    public int totalDwarves = 20;
    [FormerlySerializedAs("requiredRescues")]
    public int requiredMinedResources = 1;
    public List<LevelJobRule> jobRules = new();
    public Vector3Int originInChunks;
    public Vector3Int sizeInChunks;
    public Vector3Int gameplayBoundsMinimum;
    public Vector3Int gameplayBoundsSize;
    public List<LevelVoxelRecord> voxels = new();
    public List<LevelEntitySaveRecord> entities = new();

    public LevelSaveData Clone()
    {
        LevelSaveData result = new()
        {
            schemaVersion = schemaVersion,
            chunkSize = chunkSize,
            levelId = levelId,
            displayName = displayName,
            totalDwarves = totalDwarves,
            requiredMinedResources = requiredMinedResources,
            jobRules = CloneJobRules(jobRules),
            originInChunks = originInChunks,
            sizeInChunks = sizeInChunks,
            gameplayBoundsMinimum = gameplayBoundsMinimum,
            gameplayBoundsSize = gameplayBoundsSize,
            voxels = voxels != null
                ? new List<LevelVoxelRecord>(voxels)
                : new List<LevelVoxelRecord>(),
            entities = new List<LevelEntitySaveRecord>()
        };

        if (entities != null)
        {
            foreach (LevelEntitySaveRecord entity in entities)
            {
                if (entity != null)
                {
                    result.entities.Add(entity.Clone());
                }
            }
        }

        return result;
    }

    private static List<LevelJobRule> CloneJobRules(
        IEnumerable<LevelJobRule> source)
    {
        List<LevelJobRule> result = new();

        if (source == null)
        {
            return result;
        }

        foreach (LevelJobRule rule in source)
        {
            if (rule != null)
            {
                result.Add(rule.Clone());
            }
        }

        return result;
    }
}

[Serializable]
public sealed class LevelEntitySaveRecord
{
    public string entityId;
    public LevelEntityType type;
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;
    public Vector3Int volumeSize = Vector3Int.one;
    public PuzzleSide facing = PuzzleSide.North;
    public bool overrideMaximumFillY;
    public int maximumFillY;
    public int supplyUnitsPerTick = 128;
    public int outletCapacityOverride;
    public bool hasVisualPrefab;
    public string visualPrefabResourcePath;
    public Vector3 spawnMarkerLocalPosition;
    public int oreCapacity = 60;

    public static LevelEntitySaveRecord FromRuntime(
        LevelEntityRecord record)
    {
        if (record == null)
        {
            return null;
        }

        return new LevelEntitySaveRecord
        {
            entityId = record.EntityId,
            type = record.Type,
            position = record.Position,
            rotation = record.Rotation,
            volumeSize = record.VolumeSize,
            facing = record.Facing,
            overrideMaximumFillY = record.OverrideMaximumFillY,
            maximumFillY = record.MaximumFillY,
            supplyUnitsPerTick = record.SupplyUnitsPerTick,
            outletCapacityOverride = record.OutletCapacityOverride,
            hasVisualPrefab = record.VisualPrefab != null,
            visualPrefabResourcePath =
                record.VisualPrefabResourcePath,
            spawnMarkerLocalPosition =
                record.SpawnMarkerLocalPosition,
            oreCapacity = record.OreCapacity
        };
    }

    public LevelEntityRecord ToRuntime()
    {
        return LevelEntityRecord.FromSaveRecord(this);
    }

    public LevelEntitySaveRecord Clone()
    {
        return new LevelEntitySaveRecord
        {
            entityId = entityId,
            type = type,
            position = position,
            rotation = rotation,
            volumeSize = volumeSize,
            facing = facing,
            overrideMaximumFillY = overrideMaximumFillY,
            maximumFillY = maximumFillY,
            supplyUnitsPerTick = supplyUnitsPerTick,
            outletCapacityOverride = outletCapacityOverride,
            hasVisualPrefab = hasVisualPrefab,
            visualPrefabResourcePath = visualPrefabResourcePath,
            spawnMarkerLocalPosition = spawnMarkerLocalPosition,
            oreCapacity = oreCapacity
        };
    }
}
