using System;
using UnityEngine;

[Serializable]
public readonly struct LevelVoxelState
{
    public Vector3Int Position { get; }
    public bool HasVoxel { get; }
    public LevelVoxelRecord Record { get; }

    public LevelVoxelState(
        Vector3Int position,
        bool hasVoxel,
        LevelVoxelRecord record)
    {
        Position = position;
        HasVoxel = hasVoxel;
        Record = record;
    }

    public static LevelVoxelState Empty(Vector3Int position)
    {
        return new LevelVoxelState(position, false, default);
    }

    public static LevelVoxelState Occupied(LevelVoxelRecord record)
    {
        return new LevelVoxelState(record.Position, true, record);
    }
}
