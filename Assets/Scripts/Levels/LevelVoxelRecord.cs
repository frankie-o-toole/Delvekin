using System;
using UnityEngine;

[Serializable]
public struct LevelVoxelRecord
{
    [SerializeField]
    private Vector3Int position;

    [SerializeField]
    private VoxelType type;

    [SerializeField]
    private PuzzleSide facing;

    public Vector3Int Position => position;
    public VoxelType Type => type;
    public PuzzleSide Facing => facing;

    public LevelVoxelRecord(
        Vector3Int position,
        VoxelType type,
        PuzzleSide facing)
    {
        this.position = position;
        this.type = type;
        this.facing = facing;
    }
}
