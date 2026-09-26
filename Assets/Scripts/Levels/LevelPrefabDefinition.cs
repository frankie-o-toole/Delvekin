using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "LevelPrefabDefinition",
    menuName = "Delvekin/Level Prefab Definition")]
public sealed class LevelPrefabDefinition : ScriptableObject
{
    public const int CurrentSchemaVersion = 2;

    [SerializeField]
    private int schemaVersion = CurrentSchemaVersion;

    [SerializeField]
    private string displayName = "New Level Prefab";

    [Tooltip("Full captured volume, including implicit Air cells.")]
    [SerializeField]
    private Vector3Int size = Vector3Int.one;

    [Tooltip(
        "Sparse voxel data in local coordinates. Air is represented by " +
        "the absence of a record.")]
    [SerializeField]
    private List<LevelVoxelRecord> voxels = new();

    [Tooltip(
        "Non-voxel authoring entities stored relative to the prefab origin.")]
    [SerializeField]
    private List<LevelEntityRecord> entities = new();

    public int SchemaVersion => schemaVersion;
    public string DisplayName => displayName;
    public Vector3Int Size => size;
    public IReadOnlyList<LevelVoxelRecord> Voxels => voxels;
    public IReadOnlyList<LevelEntityRecord> Entities => entities;

    public void ReplaceContent(
        Vector3Int newSize,
        IEnumerable<LevelVoxelRecord> newVoxels,
        IEnumerable<LevelEntityRecord> newEntities)
    {
        schemaVersion = CurrentSchemaVersion;
        size = ClampSize(newSize);
        voxels = newVoxels != null
            ? new List<LevelVoxelRecord>(newVoxels)
            : new List<LevelVoxelRecord>();

        voxels.RemoveAll(
            record =>
                VoxelTraits.Has(record.Type, VoxelTrait.Empty) ||
                !ContainsLocalPosition(record.Position));

        voxels.Sort(CompareRecords);

        entities = CloneEntities(newEntities);
    }

    public bool ContainsLocalPosition(Vector3Int localPosition)
    {
        return
            localPosition.x >= 0 &&
            localPosition.y >= 0 &&
            localPosition.z >= 0 &&
            localPosition.x < size.x &&
            localPosition.y < size.y &&
            localPosition.z < size.z;
    }

    public RuntimeSnapshot CreateRuntimeSnapshot()
    {
        return new RuntimeSnapshot(
            schemaVersion,
            displayName,
            size,
            new List<LevelVoxelRecord>(voxels),
            CloneEntities(entities));
    }

    private void OnValidate()
    {
        schemaVersion = CurrentSchemaVersion;
        size = ClampSize(size);
        voxels ??= new List<LevelVoxelRecord>();
        entities ??= new List<LevelEntityRecord>();

        voxels.RemoveAll(
            record =>
                VoxelTraits.Has(record.Type, VoxelTrait.Empty) ||
                !ContainsLocalPosition(record.Position));

        voxels.Sort(CompareRecords);

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
            if (record != null)
            {
                result.Add(record.Clone());
            }
        }

        return result;
    }

    private static Vector3Int ClampSize(Vector3Int value)
    {
        return new Vector3Int(
            Mathf.Max(1, value.x),
            Mathf.Max(1, value.y),
            Mathf.Max(1, value.z));
    }

    private static int CompareRecords(
        LevelVoxelRecord left,
        LevelVoxelRecord right)
    {
        int comparison =
            left.Position.x.CompareTo(right.Position.x);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison =
            left.Position.y.CompareTo(right.Position.y);

        return comparison != 0
            ? comparison
            : left.Position.z.CompareTo(right.Position.z);
    }

    [Serializable]
    public sealed class RuntimeSnapshot
    {
        public int SchemaVersion { get; }
        public string DisplayName { get; }
        public Vector3Int Size { get; }
        public IReadOnlyList<LevelVoxelRecord> Voxels { get; }
        public IReadOnlyList<LevelEntityRecord> Entities { get; }

        public RuntimeSnapshot(
            int schemaVersion,
            string displayName,
            Vector3Int size,
            IReadOnlyList<LevelVoxelRecord> voxels,
            IReadOnlyList<LevelEntityRecord> entities)
        {
            SchemaVersion = schemaVersion;
            DisplayName = displayName;
            Size = size;
            Voxels = voxels;
            Entities = entities;
        }
    }
}
