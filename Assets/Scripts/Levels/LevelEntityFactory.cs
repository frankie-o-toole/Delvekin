using UnityEngine;

public static class LevelEntityFactory
{
    public static Component CreateEntity(
        LevelEntityRecord record,
        VoxelWorld world,
        Transform parent,
        bool runtimeCopy)
    {
        if (record == null)
        {
            return null;
        }

        return record.Type switch
        {
            LevelEntityType.WaterSource =>
                CreateWaterPortal(
                    record,
                    world,
                    parent,
                    runtimeCopy),
            LevelEntityType.WaterOutlet =>
                CreateWaterPortal(
                    record,
                    world,
                    parent,
                    runtimeCopy),
            LevelEntityType.SpawnHouse =>
                CreateSpawnHouse(
                    record,
                    world,
                    parent,
                    runtimeCopy),
            _ => null
        };
    }

    public static WaterPortal CreateWaterPortal(
        LevelEntityRecord record,
        VoxelWorld world,
        Transform parent,
        bool runtimeCopy)
    {
        if (record == null ||
            (record.Type != LevelEntityType.WaterSource &&
             record.Type != LevelEntityType.WaterOutlet))
        {
            return null;
        }

        record.EnsureValid();

        string objectName =
            record.Type == LevelEntityType.WaterSource
                ? "Water Source"
                : "Water Outlet";

        if (runtimeCopy)
        {
            objectName += " (Runtime)";
        }

        GameObject instance = new(objectName);
        instance.SetActive(false);
        instance.transform.SetParent(parent, true);

        WaterPortal portal;

        switch (record.Type)
        {
            case LevelEntityType.WaterSource:
            {
                WaterSourcePortal source =
                    instance.AddComponent<WaterSourcePortal>();

                source.ConfigureSource(
                    record.OverrideMaximumFillY,
                    record.MaximumFillY,
                    record.SupplyUnitsPerTick);

                portal = source;
                break;
            }

            case LevelEntityType.WaterOutlet:
            {
                WaterOutletPortal outlet =
                    instance.AddComponent<WaterOutletPortal>();

                outlet.ConfigureOutlet(
                    record.OutletCapacityOverride);

                portal = outlet;
                break;
            }

            default:
                Object.DestroyImmediate(instance);
                return null;
        }

        portal.ConfigureIdentity(record.EntityId);

        portal.Configure(
            world,
            record.MinimumVoxel,
            record.VolumeSize,
            record.Facing);

        instance.transform.rotation = record.Rotation;
        instance.SetActive(true);
        return portal;
    }

    public static SpawnHouseAuthoring CreateSpawnHouse(
        LevelEntityRecord record,
        VoxelWorld world,
        Transform parent,
        bool runtimeCopy)
    {
        if (record == null ||
            record.Type != LevelEntityType.SpawnHouse)
        {
            return null;
        }

        record.EnsureValid();

        string objectName = runtimeCopy
            ? "Spawn House (Runtime)"
            : "Spawn House";

        GameObject instance = new(objectName);
        instance.SetActive(false);
        instance.transform.SetParent(parent, true);

        SpawnHouseAuthoring house =
            instance.AddComponent<SpawnHouseAuthoring>();

        house.ConfigureIdentity(record.EntityId);
        house.Configure(
            world,
            record.Position,
            record.Rotation,
            record.VolumeSize,
            record.SpawnMarkerLocalPosition,
            record.Facing,
            record.VisualPrefab,
            runtimeCopy);

        instance.SetActive(true);
        return house;
    }
}
