using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "LevelDefinition",
    menuName = "Delvekin/Level Definition")]
public sealed class LevelDefinition : ScriptableObject
{
    public const int CurrentSchemaVersion = 1;

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

    public int SchemaVersion => schemaVersion;
    public string DisplayName => displayName;
    public Vector3Int OriginInChunks => originInChunks;
    public Vector3Int SizeInChunks => sizeInChunks;
    public IReadOnlyList<LevelVoxelRecord> Voxels => voxels;

    public RuntimeSnapshot CreateRuntimeSnapshot()
    {
        return new RuntimeSnapshot(
            schemaVersion,
            displayName,
            originInChunks,
            sizeInChunks,
            new List<LevelVoxelRecord>(voxels));
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

    private void OnValidate()
    {
        sizeInChunks = ClampSize(sizeInChunks);
        voxels ??= new List<LevelVoxelRecord>();
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

        public RuntimeSnapshot(
            int schemaVersion,
            string displayName,
            Vector3Int originInChunks,
            Vector3Int sizeInChunks,
            IReadOnlyList<LevelVoxelRecord> voxels)
        {
            SchemaVersion = schemaVersion;
            DisplayName = displayName;
            OriginInChunks = originInChunks;
            SizeInChunks = sizeInChunks;
            Voxels = voxels;
        }
    }
}
