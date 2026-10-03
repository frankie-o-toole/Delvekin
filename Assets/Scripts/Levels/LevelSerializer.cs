using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class LevelSerializer
{
    [Serializable]
    private sealed class SaveHeader
    {
        public int schemaVersion;
    }

    public static string GetPath(string fileName)
    {
        string safeName = Path.GetFileNameWithoutExtension(fileName);

        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "Level";
        }

        return Path.Combine(
            Application.persistentDataPath,
            safeName + ".json");
    }

    public static bool Save(LevelSaveData level, string fileName)
    {
        if (level == null)
        {
            Debug.LogError("Cannot save null level data.");
            return false;
        }

        level.schemaVersion = LevelDefinition.CurrentSchemaVersion;
        level.chunkSize = Chunk.ChunkSize;

        List<string> validationErrors = new();

        if (!LevelSaveValidator.Validate(level, validationErrors))
        {
            Debug.LogError(
                "Level save rejected:\n- " +
                string.Join("\n- ", validationErrors));
            return false;
        }

        string path = GetPath(fileName);
        string temporaryPath = path + ".tmp";

        try
        {
            string directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(level, true);
            File.WriteAllText(temporaryPath, json);

            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, null);
            }
            else
            {
                File.Move(temporaryPath, path);
            }

            Debug.Log($"Saved level schema {level.schemaVersion} to: {path}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Failed to save level to '{path}': {exception.Message}");

            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            return false;
        }
    }

    public static LevelSaveData Load(string fileName)
    {
        string path = GetPath(fileName);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"File not found: {path}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            SaveHeader header = JsonUtility.FromJson<SaveHeader>(json);

            return header != null && header.schemaVersion > 0
                ? LoadVersioned(json, path)
                : ImportLegacy(json, path);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Failed to load level from '{path}': {exception.Message}");
            return null;
        }
    }

    private static LevelSaveData LoadVersioned(
        string json,
        string path)
    {
        LevelSaveData data = JsonUtility.FromJson<LevelSaveData>(json);

        if (data == null)
        {
            throw new InvalidDataException("JSON did not contain level data.");
        }

        if (data.schemaVersion > LevelDefinition.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Level schema {data.schemaVersion} is newer than supported " +
                $"schema {LevelDefinition.CurrentSchemaVersion}.");
        }

        if (data.chunkSize != 0 && data.chunkSize != Chunk.ChunkSize)
        {
            throw new InvalidDataException(
                $"Level chunk size {data.chunkSize} does not match runtime " +
                $"chunk size {Chunk.ChunkSize}.");
        }

        int loadedVersion = data.schemaVersion;
        Normalize(data);
        data.schemaVersion = LevelDefinition.CurrentSchemaVersion;

        List<string> validationErrors = new();

        if (!LevelSaveValidator.Validate(data, validationErrors))
        {
            throw new InvalidDataException(
                "Level validation failed:\n- " +
                string.Join("\n- ", validationErrors));
        }

        if (loadedVersion < LevelDefinition.CurrentSchemaVersion)
        {
            Debug.Log(
                $"Migrated '{path}' from schema {loadedVersion} to " +
                $"schema {LevelDefinition.CurrentSchemaVersion} in memory.");
        }

        return data;
    }

    private static LevelSaveData ImportLegacy(
        string json,
        string path)
    {
#pragma warning disable CS0618
        SavedLevel legacy = JsonUtility.FromJson<SavedLevel>(json);
#pragma warning restore CS0618

        if (legacy?.voxels == null)
        {
            throw new InvalidDataException(
                "JSON is neither a versioned level nor a legacy SavedLevel.");
        }

        List<LevelVoxelRecord> voxels = new(legacy.voxels.Count);
        bool hasBounds = false;
        Vector3Int minimumChunk = Vector3Int.zero;
        Vector3Int maximumChunk = Vector3Int.zero;

#pragma warning disable CS0618
        foreach (SavedVoxel saved in legacy.voxels)
#pragma warning restore CS0618
        {
            if (saved == null ||
                VoxelTraits.Has(saved.type, VoxelTrait.Empty))
            {
                continue;
            }

            Vector3Int position = new(saved.x, saved.y, saved.z);
            Vector3Int chunk = VoxelMath.WorldToChunkCoord(position);

            if (!hasBounds)
            {
                minimumChunk = chunk;
                maximumChunk = chunk;
                hasBounds = true;
            }
            else
            {
                minimumChunk = Vector3Int.Min(minimumChunk, chunk);
                maximumChunk = Vector3Int.Max(maximumChunk, chunk);
            }

            voxels.Add(
                new LevelVoxelRecord(
                    position,
                    saved.type,
                    saved.facing,
                    WaterAmount.Full));
        }

        if (!hasBounds)
        {
            minimumChunk = Vector3Int.zero;
            maximumChunk = Vector3Int.zero;
        }

        Vector3Int size =
            maximumChunk - minimumChunk + Vector3Int.one;
        Vector3Int worldMinimum = minimumChunk * Chunk.ChunkSize;

        LevelSaveData imported = new()
        {
            schemaVersion = LevelDefinition.CurrentSchemaVersion,
            chunkSize = Chunk.ChunkSize,
            displayName = Path.GetFileNameWithoutExtension(path),
            originInChunks = minimumChunk,
            sizeInChunks = size,
            gameplayBoundsMinimum = worldMinimum,
            gameplayBoundsSize = size * Chunk.ChunkSize,
            voxels = voxels,
            entities = new List<LevelEntitySaveRecord>()
        };

        Normalize(imported);

        Debug.LogWarning(
            $"Imported legacy voxel-only level '{path}'. Bounds were inferred; " +
            "legacy files contain no entities or half-water amounts. Saving " +
            "again upgrades the file to the current schema.");

        return imported;
    }

    private static void Normalize(LevelSaveData data)
    {
        data.chunkSize = Chunk.ChunkSize;
        data.levelId = string.IsNullOrWhiteSpace(data.levelId)
            ? Guid.NewGuid().ToString("N")
            : data.levelId.Trim();
        data.displayName = string.IsNullOrWhiteSpace(data.displayName)
            ? "Loaded Level"
            : data.displayName;
        data.totalDwarves = Mathf.Max(1, data.totalDwarves);
        data.requiredRescues = Mathf.Clamp(
            data.requiredRescues,
            1,
            data.totalDwarves);
        data.jobRules ??= new List<LevelJobRule>();

        Dictionary<DwarfJobType, LevelJobRule> normalizedRules = new();

        foreach (LevelJobRule rule in data.jobRules)
        {
            if (rule == null || rule.jobType == DwarfJobType.None)
            {
                continue;
            }

            rule.EnsureValid();
            normalizedRules[rule.jobType] = rule;
        }

        data.jobRules = new List<LevelJobRule>(normalizedRules.Values);
        data.sizeInChunks = new Vector3Int(
            Mathf.Max(1, data.sizeInChunks.x),
            Mathf.Max(1, data.sizeInChunks.y),
            Mathf.Max(1, data.sizeInChunks.z));
        data.voxels ??= new List<LevelVoxelRecord>();
        data.entities ??= new List<LevelEntitySaveRecord>();

        if (data.gameplayBoundsSize.x <= 0 ||
            data.gameplayBoundsSize.y <= 0 ||
            data.gameplayBoundsSize.z <= 0)
        {
            data.gameplayBoundsMinimum =
                data.originInChunks * Chunk.ChunkSize;
            data.gameplayBoundsSize =
                data.sizeInChunks * Chunk.ChunkSize;
        }
    }
}
