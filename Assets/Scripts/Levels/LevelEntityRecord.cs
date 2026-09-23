using System;
using UnityEngine;

public enum LevelEntityType : byte
{
    WaterSource,
    WaterOutlet
}

/// <summary>
/// Persistent, level-owned description of a non-voxel gameplay entity.
/// Common transform data is intentionally shared so future prefab-backed
/// entities (Spawn House, Exit, Mine, Waterfall) can use the same pipeline.
/// </summary>
[Serializable]
public sealed class LevelEntityRecord
{
    [SerializeField]
    private string entityId;

    [SerializeField]
    private LevelEntityType type;

    [SerializeField]
    private Vector3 position;

    [SerializeField]
    private Quaternion rotation = Quaternion.identity;

    [SerializeField]
    private Vector3Int volumeSize = Vector3Int.one;

    [SerializeField]
    private PuzzleSide facing = PuzzleSide.North;

    [SerializeField]
    private bool overrideMaximumFillY;

    [SerializeField]
    private int maximumFillY;

    [SerializeField]
    private int supplyUnitsPerTick = 128;

    [SerializeField]
    private int outletCapacityOverride;

    public string EntityId => entityId;
    public LevelEntityType Type => type;
    public Vector3 Position => position;
    public Quaternion Rotation => rotation;
    public Vector3Int VolumeSize => volumeSize;
    public PuzzleSide Facing => facing;
    public bool OverrideMaximumFillY => overrideMaximumFillY;
    public int MaximumFillY => maximumFillY;
    public int SupplyUnitsPerTick => Mathf.Max(1, supplyUnitsPerTick);
    public int OutletCapacityOverride => Mathf.Max(0, outletCapacityOverride);

    public Vector3Int MinimumVoxel =>
        Vector3Int.FloorToInt(
            position - (Vector3)volumeSize * 0.5f +
            Vector3.one * 0.001f);

    public static LevelEntityRecord FromPortal(WaterPortal portal)
    {
        if (portal == null)
        {
            return null;
        }

        LevelEntityRecord record = new()
        {
            entityId = portal.EntityId,
            position = portal.transform.position,
            rotation = portal.transform.rotation,
            volumeSize = portal.Size,
            facing = portal.Facing
        };

        if (portal is WaterSourcePortal source)
        {
            record.type = LevelEntityType.WaterSource;
            record.overrideMaximumFillY =
                source.OverrideMaximumFillY;
            record.maximumFillY = source.MaximumFillY;
            record.supplyUnitsPerTick =
                source.SupplyUnitsPerTick;
        }
        else if (portal is WaterOutletPortal outlet)
        {
            record.type = LevelEntityType.WaterOutlet;
            record.outletCapacityOverride =
                outlet.CapacityOverride;
        }
        else
        {
            return null;
        }

        return record;
    }

    public LevelEntityRecord Clone()
    {
        return new LevelEntityRecord
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
            outletCapacityOverride = outletCapacityOverride
        };
    }

    public void EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            entityId = Guid.NewGuid().ToString("N");
        }

        volumeSize = new Vector3Int(
            Mathf.Max(1, volumeSize.x),
            Mathf.Max(1, volumeSize.y),
            Mathf.Max(1, volumeSize.z));

        supplyUnitsPerTick = Mathf.Max(1, supplyUnitsPerTick);
        outletCapacityOverride = Mathf.Max(0, outletCapacityOverride);

        if (rotation == default)
        {
            rotation = Quaternion.identity;
        }
    }
}
