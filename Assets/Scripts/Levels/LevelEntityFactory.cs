using UnityEngine;

public static class LevelEntityFactory
{
    public static WaterPortal CreateWaterPortal(
        LevelEntityRecord record,
        VoxelWorld world,
        Transform parent,
        bool runtimeCopy)
    {
        if (record == null)
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
}
