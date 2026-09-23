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

    [SerializeField]
    private WaterAmount waterAmount;

    public Vector3Int Position => position;
    public VoxelType Type => type;
    public PuzzleSide Facing => facing;

    public WaterAmount Amount =>
        type == VoxelType.Water &&
        waterAmount == global::WaterAmount.Half
            ? global::WaterAmount.Half
            : global::WaterAmount.Full;

    public LevelVoxelRecord(
        Vector3Int position,
        VoxelType type,
        PuzzleSide facing,
        WaterAmount waterAmount = global::WaterAmount.Full)
    {
        this.position = position;
        this.type = type;
        this.facing = facing;
        this.waterAmount =
            type == VoxelType.Water &&
            waterAmount == global::WaterAmount.Half
                ? global::WaterAmount.Half
                : global::WaterAmount.Full;
    }
}
