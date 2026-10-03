using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "LevelDefinition",
    menuName = "Delvekin/Level Definition")]
public sealed class LevelDefinition : ScriptableObject
{
    public const int CurrentSchemaVersion = 9;

    [HideInInspector]
    [SerializeField]
    private int schemaVersion = CurrentSchemaVersion;

    [SerializeField]
    private string levelId;

    [SerializeField]
    private string displayName = "New Level";

    [Header("Gameplay Rules")]
    [Min(1)]
    [SerializeField]
    private int totalDwarves = 20;

    [Min(1)]
    [SerializeField]
    [FormerlySerializedAs("requiredRescues")]
    private int requiredMinedResources = 1;

    [Tooltip("Jobs absent from this list are unavailable in this level. " +
             "Defaults are used until player loadouts are implemented.")]
    [SerializeField]
    private List<LevelJobRule> jobRules = new();

    [Tooltip("Lowest chunk coordinate belonging to this level.")]
    [SerializeField]
    private Vector3Int originInChunks = Vector3Int.zero;

    [Tooltip("Number of chunks on each axis.")]
    [SerializeField]
    private Vector3Int sizeInChunks = Vector3Int.one;

    [Tooltip("Exact voxel-space bounds used for gameplay loss checks. " +
             "These bounds must remain inside the chunk-aligned world bounds.")]
    [SerializeField]
    private Vector3Int gameplayBoundsMinimum;

    [SerializeField]
    private Vector3Int gameplayBoundsSize;

    [Tooltip("Sparse authored data. Air is represented by the absence of a record.")]
    [SerializeField]
    private List<LevelVoxelRecord> voxels = new();

    [Tooltip("Persistent non-voxel gameplay entities owned by this level.")]
    [SerializeField]
    private List<LevelEntityRecord> entities = new();

    public int SchemaVersion => schemaVersion;
    public string LevelId => levelId;
    public string DisplayName => displayName;
    public int TotalDwarves => totalDwarves;
    public int RequiredMinedResources => requiredMinedResources;
    public IReadOnlyList<LevelJobRule> JobRules => jobRules;
    public Vector3Int OriginInChunks => originInChunks;
    public Vector3Int SizeInChunks => sizeInChunks;
    public Vector3Int GameplayBoundsMinimum =>
        HasValidGameplayBounds
            ? gameplayBoundsMinimum
            : WorldMinimumVoxel;
    public Vector3Int GameplayBoundsSize =>
        HasValidGameplayBounds
            ? gameplayBoundsSize
            : WorldSizeInVoxels;
    public Vector3Int GameplayBoundsMaximumExclusive =>
        GameplayBoundsMinimum + GameplayBoundsSize;
    public IReadOnlyList<LevelVoxelRecord> Voxels => voxels;
    public IReadOnlyList<LevelEntityRecord> Entities => entities;
    public int TotalOreCapacity => CalculateTotalOreCapacity(entities);

    public RuntimeSnapshot CreateRuntimeSnapshot()
    {
        return new RuntimeSnapshot(
            schemaVersion,
            levelId,
            displayName,
            totalDwarves,
            requiredMinedResources,
            CloneJobRules(jobRules),
            originInChunks,
            sizeInChunks,
            GameplayBoundsMinimum,
            GameplayBoundsSize,
            new List<LevelVoxelRecord>(voxels),
            CloneEntities(entities));
    }

    public LevelSaveData CreateSaveData()
    {
        LevelSaveData data = new()
        {
            schemaVersion = CurrentSchemaVersion,
            chunkSize = Chunk.ChunkSize,
            levelId = levelId,
            displayName = displayName,
            totalDwarves = totalDwarves,
            requiredMinedResources = requiredMinedResources,
            jobRules = CloneJobRules(jobRules),
            originInChunks = originInChunks,
            sizeInChunks = sizeInChunks,
            gameplayBoundsMinimum = GameplayBoundsMinimum,
            gameplayBoundsSize = GameplayBoundsSize,
            voxels = new List<LevelVoxelRecord>(voxels),
            entities = new List<LevelEntitySaveRecord>()
        };

        foreach (LevelEntityRecord entity in entities)
        {
            LevelEntitySaveRecord saved =
                LevelEntitySaveRecord.FromRuntime(entity);

            if (saved != null)
            {
                data.entities.Add(saved);
            }
        }

        return data;
    }

    public void ReplaceAllContent(LevelSaveData data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (data.chunkSize != 0 &&
            data.chunkSize != Chunk.ChunkSize)
        {
            throw new InvalidOperationException(
                $"Level chunk size {data.chunkSize} is incompatible with " +
                $"runtime chunk size {Chunk.ChunkSize}.");
        }

        schemaVersion = CurrentSchemaVersion;
        levelId = string.IsNullOrWhiteSpace(data.levelId)
            ? Guid.NewGuid().ToString("N")
            : data.levelId.Trim();
        displayName = string.IsNullOrWhiteSpace(data.displayName)
            ? "Imported Level"
            : data.displayName;
        totalDwarves = Mathf.Max(1, data.totalDwarves);
        requiredMinedResources = Mathf.Clamp(
            data.requiredMinedResources,
            1,
            totalDwarves);
        jobRules = CloneJobRules(data.jobRules);
        originInChunks = data.originInChunks;
        sizeInChunks = ClampSize(data.sizeInChunks);
        gameplayBoundsMinimum = data.gameplayBoundsMinimum;
        gameplayBoundsSize = data.gameplayBoundsSize;
        EnsureGameplayBoundsInsideWorld();

        voxels = data.voxels != null
            ? new List<LevelVoxelRecord>(data.voxels)
            : new List<LevelVoxelRecord>();
        voxels.RemoveAll(
            record =>
                !ContainsWorldPosition(record.Position) ||
                VoxelTraits.Has(record.Type, VoxelTrait.Empty));
        voxels.Sort(CompareRecords);

        List<LevelEntityRecord> importedEntities = new();

        if (data.entities != null)
        {
            foreach (LevelEntitySaveRecord saved in data.entities)
            {
                LevelEntityRecord entity = saved?.ToRuntime();

                if (entity != null)
                {
                    importedEntities.Add(entity);
                }
            }
        }

        entities = CloneEntities(importedEntities);
    }

    public void ReplaceContent(
        Vector3Int newOriginInChunks,
        Vector3Int newSizeInChunks,
        IEnumerable<LevelVoxelRecord> newVoxels)
    {
        schemaVersion = CurrentSchemaVersion;
        originInChunks = newOriginInChunks;
        sizeInChunks = ClampSize(newSizeInChunks);
        EnsureGameplayBoundsInsideWorld();
        voxels = newVoxels != null
            ? new List<LevelVoxelRecord>(newVoxels)
            : new List<LevelVoxelRecord>();
    }

    public void ReplaceEntities(
        IEnumerable<LevelEntityRecord> newEntities)
    {
        entities = CloneEntities(newEntities);
        schemaVersion = CurrentSchemaVersion;
    }

    public void UpsertEntity(LevelEntityRecord record)
    {
        if (record == null)
        {
            return;
        }

        record.EnsureValid();
        entities ??= new List<LevelEntityRecord>();

        for (int index = 0; index < entities.Count; index++)
        {
            if (entities[index] != null &&
                entities[index].EntityId == record.EntityId)
            {
                entities[index] = record.Clone();
                schemaVersion = CurrentSchemaVersion;
                return;
            }
        }

        entities.Add(record.Clone());
        schemaVersion = CurrentSchemaVersion;
    }

    public bool RemoveEntity(string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId) ||
            entities == null)
        {
            return false;
        }

        int removed = entities.RemoveAll(
            record =>
                record != null &&
                record.EntityId == entityId);

        return removed > 0;
    }

    public bool TryGetVoxelRecord(
        Vector3Int worldPosition,
        out LevelVoxelRecord record)
    {
        foreach (LevelVoxelRecord candidate in voxels)
        {
            if (candidate.Position == worldPosition)
            {
                record = candidate;
                return true;
            }
        }

        record = default;
        return false;
    }

    public List<LevelVoxelState> CaptureStates(
        IReadOnlyCollection<Vector3Int> positions)
    {
        List<LevelVoxelState> result = new();

        if (positions == null || positions.Count == 0)
        {
            return result;
        }

        HashSet<Vector3Int> requested = new(positions);
        Dictionary<Vector3Int, LevelVoxelRecord> existing = new();

        foreach (LevelVoxelRecord record in voxels)
        {
            if (requested.Contains(record.Position))
            {
                existing[record.Position] = record;
            }
        }

        result.Capacity = requested.Count;

        foreach (Vector3Int position in requested)
        {
            result.Add(
                existing.TryGetValue(
                    position,
                    out LevelVoxelRecord record)
                    ? LevelVoxelState.Occupied(record)
                    : LevelVoxelState.Empty(position));
        }

        return result;
    }

    public void RestoreStates(
        IReadOnlyCollection<LevelVoxelState> states)
    {
        if (states == null || states.Count == 0)
        {
            return;
        }

        Dictionary<Vector3Int, LevelVoxelRecord> byPosition = new();

        foreach (LevelVoxelRecord record in voxels)
        {
            if (ContainsWorldPosition(record.Position) &&
                !VoxelTraits.Has(record.Type, VoxelTrait.Empty))
            {
                byPosition[record.Position] = record;
            }
        }

        foreach (LevelVoxelState state in states)
        {
            if (!ContainsWorldPosition(state.Position))
            {
                continue;
            }

            if (state.HasVoxel &&
                !VoxelTraits.Has(state.Record.Type, VoxelTrait.Empty))
            {
                byPosition[state.Position] = state.Record;
            }
            else
            {
                byPosition.Remove(state.Position);
            }
        }

        voxels = new List<LevelVoxelRecord>(byPosition.Values);
        voxels.Sort(CompareRecords);
    }

    public int SetVoxels(
        IReadOnlyCollection<Vector3Int> positions,
        VoxelType type,
        PuzzleSide facing,
        WaterAmount waterAmount)
    {
        if (positions == null || positions.Count == 0)
        {
            return 0;
        }

        Dictionary<Vector3Int, LevelVoxelRecord> byPosition = new();

        foreach (LevelVoxelRecord record in voxels)
        {
            if (ContainsWorldPosition(record.Position) &&
                !VoxelTraits.Has(record.Type, VoxelTrait.Empty))
            {
                byPosition[record.Position] = record;
            }
        }

        int changed = 0;

        foreach (Vector3Int position in positions)
        {
            if (!ContainsWorldPosition(position))
            {
                continue;
            }

            if (VoxelTraits.Has(type, VoxelTrait.Empty))
            {
                if (byPosition.Remove(position))
                {
                    changed++;
                }

                continue;
            }

            LevelVoxelRecord replacement =
                new(position, type, facing, waterAmount);

            if (byPosition.TryGetValue(
                    position,
                    out LevelVoxelRecord current) &&
                RecordsMatch(current, replacement))
            {
                continue;
            }

            byPosition[position] = replacement;
            changed++;
        }

        if (changed == 0)
        {
            return 0;
        }

        voxels = new List<LevelVoxelRecord>(byPosition.Values);
        voxels.Sort(CompareRecords);
        return changed;
    }

    public bool ContainsWorldPosition(Vector3Int worldPosition)
    {
        return ContainsWorldPosition(
            worldPosition,
            originInChunks,
            sizeInChunks);
    }

    public bool ContainsGameplayPosition(Vector3Int worldPosition)
    {
        return ContainsPosition(
            worldPosition,
            GameplayBoundsMinimum,
            GameplayBoundsMaximumExclusive);
    }

    public bool TrySetBounds(
        Vector3Int newOriginInChunks,
        Vector3Int newSizeInChunks,
        out string failureReason)
    {
        newSizeInChunks = ClampSize(newSizeInChunks);

        Vector3Int newWorldMinimum =
            newOriginInChunks * Chunk.ChunkSize;

        Vector3Int newWorldMaximumExclusive =
            (newOriginInChunks + newSizeInChunks) * Chunk.ChunkSize;

        if (!ContainsBounds(
                newWorldMinimum,
                newWorldMaximumExclusive,
                GameplayBoundsMinimum,
                GameplayBoundsMaximumExclusive))
        {
            failureReason =
                "The current gameplay bounds would fall outside the new " +
                "world bounds. Resize or move the gameplay bounds first.";
            return false;
        }

        foreach (LevelVoxelRecord record in voxels)
        {
            if (!ContainsWorldPosition(
                    record.Position,
                    newOriginInChunks,
                    newSizeInChunks))
            {
                failureReason =
                    $"Voxel at {record.Position} would fall outside the " +
                    "new world bounds.";
                return false;
            }
        }

        foreach (LevelEntityRecord entity in entities)
        {
            if (entity == null)
            {
                continue;
            }

            if (entity.Type == LevelEntityType.SpawnHouse)
            {
                if (!ContainsWorldPosition(
                        entity.SpawnVoxel,
                        newOriginInChunks,
                        newSizeInChunks))
                {
                    failureReason =
                        $"Spawn House marker at {entity.SpawnVoxel} would " +
                        "fall outside the new world bounds.";
                    return false;
                }

                continue;
            }

            Vector3Int maximumInclusive =
                (newOriginInChunks + newSizeInChunks) *
                Chunk.ChunkSize - Vector3Int.one;

            if (!entity.IsFullyInside(
                    newOriginInChunks * Chunk.ChunkSize,
                    maximumInclusive))
            {
                failureReason =
                    $"{entity.Type} '{entity.EntityId}' would fall outside " +
                    "the new world bounds.";
                return false;
            }
        }

        originInChunks = newOriginInChunks;
        sizeInChunks = newSizeInChunks;
        schemaVersion = CurrentSchemaVersion;
        failureReason = null;
        return true;
    }

    public bool TrySetGameplayBounds(
        Vector3Int minimum,
        Vector3Int size,
        out string failureReason)
    {
        size = new Vector3Int(
            Mathf.Max(1, size.x),
            Mathf.Max(1, size.y),
            Mathf.Max(1, size.z));

        Vector3Int maximumExclusive = minimum + size;

        if (!ContainsBounds(
                WorldMinimumVoxel,
                WorldMaximumExclusiveVoxel,
                minimum,
                maximumExclusive))
        {
            failureReason =
                "Gameplay bounds must remain completely inside the " +
                "chunk-aligned world bounds.";
            return false;
        }

        foreach (LevelVoxelRecord record in voxels)
        {
            if (VoxelTraits.Has(
                    record.Type,
                    VoxelTrait.GameplayMarker) &&
                !ContainsPosition(
                    record.Position,
                    minimum,
                    maximumExclusive))
            {
                failureReason =
                    $"{record.Type} at {record.Position} would fall outside " +
                    "the gameplay bounds.";
                return false;
            }
        }

        foreach (LevelEntityRecord entity in entities)
        {
            if (entity != null &&
                entity.Type == LevelEntityType.SpawnHouse &&
                !ContainsPosition(
                    entity.SpawnVoxel,
                    minimum,
                    maximumExclusive))
            {
                failureReason =
                    $"Spawn House marker at {entity.SpawnVoxel} would fall " +
                    "outside the gameplay bounds.";
                return false;
            }
        }

        gameplayBoundsMinimum = minimum;
        gameplayBoundsSize = size;
        schemaVersion = CurrentSchemaVersion;
        failureReason = null;
        return true;
    }

    private static bool ContainsWorldPosition(
        Vector3Int worldPosition,
        Vector3Int boundsOriginInChunks,
        Vector3Int boundsSizeInChunks)
    {
        Vector3Int minimum =
            boundsOriginInChunks * Chunk.ChunkSize;

        Vector3Int maximumExclusive =
            minimum + boundsSizeInChunks * Chunk.ChunkSize;

        return
            worldPosition.x >= minimum.x &&
            worldPosition.y >= minimum.y &&
            worldPosition.z >= minimum.z &&
            worldPosition.x < maximumExclusive.x &&
            worldPosition.y < maximumExclusive.y &&
            worldPosition.z < maximumExclusive.z;
    }

    private Vector3Int WorldMinimumVoxel =>
        originInChunks * Chunk.ChunkSize;

    private Vector3Int WorldSizeInVoxels =>
        sizeInChunks * Chunk.ChunkSize;

    private Vector3Int WorldMaximumExclusiveVoxel =>
        WorldMinimumVoxel + WorldSizeInVoxels;

    private bool HasValidGameplayBounds =>
        gameplayBoundsSize.x > 0 &&
        gameplayBoundsSize.y > 0 &&
        gameplayBoundsSize.z > 0 &&
        ContainsBounds(
            WorldMinimumVoxel,
            WorldMaximumExclusiveVoxel,
            gameplayBoundsMinimum,
            gameplayBoundsMinimum + gameplayBoundsSize);

    private void EnsureGameplayBoundsInsideWorld()
    {
        if (HasValidGameplayBounds)
        {
            return;
        }

        gameplayBoundsMinimum = WorldMinimumVoxel;
        gameplayBoundsSize = WorldSizeInVoxels;
    }

    private static bool ContainsBounds(
        Vector3Int outerMinimum,
        Vector3Int outerMaximumExclusive,
        Vector3Int innerMinimum,
        Vector3Int innerMaximumExclusive)
    {
        return
            innerMinimum.x >= outerMinimum.x &&
            innerMinimum.y >= outerMinimum.y &&
            innerMinimum.z >= outerMinimum.z &&
            innerMaximumExclusive.x <= outerMaximumExclusive.x &&
            innerMaximumExclusive.y <= outerMaximumExclusive.y &&
            innerMaximumExclusive.z <= outerMaximumExclusive.z;
    }

    private static bool ContainsPosition(
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

    private static bool RecordsMatch(
        LevelVoxelRecord a,
        LevelVoxelRecord b)
    {
        return
            a.Position == b.Position &&
            a.Type == b.Type &&
            a.Facing == b.Facing &&
            a.Amount == b.Amount;
    }

    private static int CompareRecords(
        LevelVoxelRecord a,
        LevelVoxelRecord b)
    {
        int x = a.Position.x.CompareTo(b.Position.x);

        if (x != 0)
        {
            return x;
        }

        int y = a.Position.y.CompareTo(b.Position.y);

        return y != 0
            ? y
            : a.Position.z.CompareTo(b.Position.z);
    }

    private void OnValidate()
    {
        if (schemaVersion < CurrentSchemaVersion)
        {
            // Version 2 adds authored Half/Full water. Version 3 adds
            // level-owned authoring entities. Version 4 adds Spawn Houses.
            // Version 5 adds exact voxel-space gameplay/kill bounds.
            // Version 6 is the unified, portable level-save schema.
            // Version 7 adds stable identity and authored gameplay rules.
            // Version 8 adds finite Ore Rock authoring entities.
            // Version 9 replaces rescue goals with mined-resource goals.
            schemaVersion = CurrentSchemaVersion;
        }

        if (string.IsNullOrWhiteSpace(levelId))
        {
            levelId = Guid.NewGuid().ToString("N");
        }

        totalDwarves = Mathf.Max(1, totalDwarves);
        requiredMinedResources = Mathf.Clamp(
            requiredMinedResources,
            1,
            totalDwarves);
        jobRules ??= new List<LevelJobRule>();

        foreach (LevelJobRule rule in jobRules)
        {
            rule?.EnsureValid();
        }
        sizeInChunks = ClampSize(sizeInChunks);
        EnsureGameplayBoundsInsideWorld();
        voxels ??= new List<LevelVoxelRecord>();
        entities ??= new List<LevelEntityRecord>();

        foreach (LevelEntityRecord entity in entities)
        {
            entity?.EnsureValid();
        }
    }

    private static List<LevelEntityRecord> CloneEntities(
        IEnumerable<LevelEntityRecord> source)
    {
        List<LevelEntityRecord> result = new();

        if (source == null)
        {
            return result;
        }

        foreach (LevelEntityRecord record in source)
        {
            if (record == null)
            {
                continue;
            }

            record.EnsureValid();
            result.Add(record.Clone());
        }

        return result;
    }

    private static int CalculateTotalOreCapacity(
        IEnumerable<LevelEntityRecord> source)
    {
        int total = 0;

        if (source == null)
        {
            return total;
        }

        foreach (LevelEntityRecord entity in source)
        {
            if (entity != null &&
                entity.Type == LevelEntityType.OreRock)
            {
                total += entity.OreCapacity;
            }
        }

        return total;
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
            if (rule == null || rule.jobType == DwarfJobType.None)
            {
                continue;
            }

            LevelJobRule clone = rule.Clone();
            clone.EnsureValid();
            result.Add(clone);
        }

        return result;
    }

    private static Vector3Int ClampSize(Vector3Int size)
    {
        return new Vector3Int(
            Mathf.Max(1, size.x),
            Mathf.Max(1, size.y),
            Mathf.Max(1, size.z));
    }

    public sealed class RuntimeSnapshot
    {
        public int SchemaVersion { get; }
        public string LevelId { get; }
        public string DisplayName { get; }
        public int TotalDwarves { get; }
        public int RequiredMinedResources { get; }
        public IReadOnlyList<LevelJobRule> JobRules { get; }
        public Vector3Int OriginInChunks { get; }
        public Vector3Int SizeInChunks { get; }
        public Vector3Int GameplayBoundsMinimum { get; }
        public Vector3Int GameplayBoundsSize { get; }
        public IReadOnlyList<LevelVoxelRecord> Voxels { get; }
        public IReadOnlyList<LevelEntityRecord> Entities { get; }
        public int TotalOreCapacity =>
            CalculateTotalOreCapacity(Entities);

        public RuntimeSnapshot(
            int schemaVersion,
            string levelId,
            string displayName,
            int totalDwarves,
            int requiredMinedResources,
            IReadOnlyList<LevelJobRule> jobRules,
            Vector3Int originInChunks,
            Vector3Int sizeInChunks,
            Vector3Int gameplayBoundsMinimum,
            Vector3Int gameplayBoundsSize,
            IReadOnlyList<LevelVoxelRecord> voxels,
            IReadOnlyList<LevelEntityRecord> entities)
        {
            SchemaVersion = schemaVersion;
            LevelId = levelId;
            DisplayName = displayName;
            TotalDwarves = totalDwarves;
            RequiredMinedResources = requiredMinedResources;
            JobRules = jobRules;
            OriginInChunks = originInChunks;
            SizeInChunks = sizeInChunks;
            GameplayBoundsMinimum = gameplayBoundsMinimum;
            GameplayBoundsSize = gameplayBoundsSize;
            Voxels = voxels;
            Entities = entities;
        }
    }
}
