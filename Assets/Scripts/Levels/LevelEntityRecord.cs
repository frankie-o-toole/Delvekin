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

    public bool IsFullyInside(
        Vector3Int minimum,
        Vector3Int maximumInclusive)
    {
        Vector3Int entityMinimum = MinimumVoxel;
        Vector3Int entityMaximum =
            entityMinimum + volumeSize - Vector3Int.one;

        return
            entityMinimum.x >= minimum.x &&
            entityMinimum.y >= minimum.y &&
            entityMinimum.z >= minimum.z &&
            entityMaximum.x <= maximumInclusive.x &&
            entityMaximum.y <= maximumInclusive.y &&
            entityMaximum.z <= maximumInclusive.z;
    }

    public LevelEntityRecord CreatePrefabLocal(
        Vector3Int prefabOrigin)
    {
        LevelEntityRecord result = Clone();
        result.position -= (Vector3)prefabOrigin;

        if (result.overrideMaximumFillY)
        {
            result.maximumFillY -= prefabOrigin.y;
        }

        return result;
    }

    public LevelEntityRecord CreatePlacedCopy(
        Vector3Int placementOrigin,
        Vector3Int sourceSize,
        int quarterTurns,
        bool createUniqueId)
    {
        int turns = ((quarterTurns % 4) + 4) % 4;
        LevelEntityRecord result = Clone();
        Vector3 localPosition = position;

        if (createUniqueId)
        {
            result.entityId = Guid.NewGuid().ToString("N");
        }

        result.position =
            (Vector3)placementOrigin +
            RotateLocalPosition(
                localPosition,
                sourceSize,
                turns);

        result.volumeSize = turns % 2 == 0
            ? volumeSize
            : new Vector3Int(
                volumeSize.z,
                volumeSize.y,
                volumeSize.x);

        result.facing = RotateFacing(facing, turns);
        result.rotation =
            Quaternion.Euler(0f, 90f * turns, 0f) *
            rotation;

        if (result.overrideMaximumFillY)
        {
            result.maximumFillY += placementOrigin.y;
        }

        result.EnsureValid();
        return result;
    }

    private static Vector3 RotateLocalPosition(
        Vector3 position,
        Vector3Int sourceSize,
        int quarterTurns)
    {
        return quarterTurns switch
        {
            1 => new Vector3(
                position.z,
                position.y,
                sourceSize.x - position.x),
            2 => new Vector3(
                sourceSize.x - position.x,
                position.y,
                sourceSize.z - position.z),
            3 => new Vector3(
                sourceSize.z - position.z,
                position.y,
                position.x),
            _ => position
        };
    }

    private static PuzzleSide RotateFacing(
        PuzzleSide value,
        int quarterTurns)
    {
        string[] horizontal =
        {
            "North",
            "East",
            "South",
            "West"
        };

        int current = Array.IndexOf(
            horizontal,
            value.ToString());

        if (current < 0)
        {
            return value;
        }

        string rotatedName =
            horizontal[(current + quarterTurns) % horizontal.Length];

        return Enum.TryParse(
            rotatedName,
            out PuzzleSide rotated)
            ? rotated
            : value;
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
