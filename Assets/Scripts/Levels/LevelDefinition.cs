using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "LevelDefinition",
    menuName = "Delvekin/Level Definition")]
public sealed class LevelDefinition : ScriptableObject
{
    public const int CurrentSchemaVersion = 4;

    [SerializeField]
    private int schemaVersion = CurrentSchemaVersion;

    [SerializeField]
    private string displayName = "New Level";

    [Tooltip("Lowest chunk coordinate belonging to this level.")]
    [SerializeField]
    private Vector3Int originInChunks = Vector3Int.zero;

    [Tooltip("Number of chunks on each axis.")]
    [SerializeField]
    private Vector3Int sizeInChunks = Vector3Int.one;

    [Tooltip("Sparse authored data. Air is represented by the absence of a record.")]
    [SerializeField]
    private List<LevelVoxelRecord> voxels = new();

    [Tooltip("Persistent non-voxel gameplay entities owned by this level.")]
    [SerializeField]
    private List<LevelEntityRecord> entities = new();

    public int SchemaVersion => schemaVersion;
    public string DisplayName => displayName;
    public Vector3Int OriginInChunks => originInChunks;
    public Vector3Int SizeInChunks => sizeInChunks;
    public IReadOnlyList<LevelVoxelRecord> Voxels => voxels;
    public IReadOnlyList<LevelEntityRecord> Entities => entities;

    public RuntimeSnapshot CreateRuntimeSnapshot()
    {
        return new RuntimeSnapshot(
            schemaVersion,
            displayName,
            originInChunks,
            sizeInChunks,
            new List<LevelVoxelRecord>(voxels),
            CloneEntities(entities));
    }

    public void ReplaceContent(
        Vector3Int newOriginInChunks,
        Vector3Int newSizeInChunks,
        IEnumerable<LevelVoxelRecord> newVoxels)
    {
        schemaVersion = CurrentSchemaVersion;
        originInChunks = newOriginInChunks;
        sizeInChunks = ClampSize(newSizeInChunks);
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
                record.Type != VoxelType.Air)
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
                state.Record.Type != VoxelType.Air)
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
                record.Type != VoxelType.Air)
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

            if (type == VoxelType.Air)
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
        Vector3Int minimum = originInChunks * Chunk.ChunkSize;
        Vector3Int maximumExclusive =
            minimum + sizeInChunks * Chunk.ChunkSize;

        return
            worldPosition.x >= minimum.x &&
            worldPosition.y >= minimum.y &&
            worldPosition.z >= minimum.z &&
            worldPosition.x < maximumExclusive.x &&
            worldPosition.y < maximumExclusive.y &&
            worldPosition.z < maximumExclusive.z;
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
            // level-owned authoring entities. Version 4 adds Spawn Houses
            // with a visual reference and local spawn marker.
            schemaVersion = CurrentSchemaVersion;
        }

        sizeInChunks = ClampSize(sizeInChunks);
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
        public string DisplayName { get; }
        public Vector3Int OriginInChunks { get; }
        public Vector3Int SizeInChunks { get; }
        public IReadOnlyList<LevelVoxelRecord> Voxels { get; }
        public IReadOnlyList<LevelEntityRecord> Entities { get; }

        public RuntimeSnapshot(
            int schemaVersion,
            string displayName,
            Vector3Int originInChunks,
            Vector3Int sizeInChunks,
            IReadOnlyList<LevelVoxelRecord> voxels,
            IReadOnlyList<LevelEntityRecord> entities)
        {
            SchemaVersion = schemaVersion;
            DisplayName = displayName;
            OriginInChunks = originInChunks;
            SizeInChunks = sizeInChunks;
            Voxels = voxels;
            Entities = entities;
        }
    }
}
