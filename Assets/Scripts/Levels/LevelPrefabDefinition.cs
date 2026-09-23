using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "LevelPrefabDefinition",
    menuName = "Delvekin/Level Prefab Definition")]
public sealed class LevelPrefabDefinition : ScriptableObject
{
    public const int CurrentSchemaVersion = 1;

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

    public int SchemaVersion => schemaVersion;
    public string DisplayName => displayName;
    public Vector3Int Size => size;
    public IReadOnlyList<LevelVoxelRecord> Voxels => voxels;

    public void ReplaceContent(
        Vector3Int newSize,
        IEnumerable<LevelVoxelRecord> newVoxels)
    {
        schemaVersion = CurrentSchemaVersion;
        size = ClampSize(newSize);
        voxels = newVoxels != null
            ? new List<LevelVoxelRecord>(newVoxels)
            : new List<LevelVoxelRecord>();

        voxels.RemoveAll(
            record =>
                record.Type == VoxelType.Air ||
                !ContainsLocalPosition(record.Position));

        voxels.Sort(CompareRecords);
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
            new List<LevelVoxelRecord>(voxels));
    }

    private void OnValidate()
    {
        schemaVersion = CurrentSchemaVersion;
        size = ClampSize(size);
        voxels ??= new List<LevelVoxelRecord>();

        voxels.RemoveAll(
            record =>
                record.Type == VoxelType.Air ||
                !ContainsLocalPosition(record.Position));

        voxels.Sort(CompareRecords);
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

        public RuntimeSnapshot(
            int schemaVersion,
            string displayName,
            Vector3Int size,
            IReadOnlyList<LevelVoxelRecord> voxels)
        {
            SchemaVersion = schemaVersion;
            DisplayName = displayName;
            Size = size;
            Voxels = voxels;
        }
    }
}
