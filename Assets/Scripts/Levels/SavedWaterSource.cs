using System;
using UnityEngine;

[Serializable]
public class SavedWaterSource
{
    public int x;
    public int y;
    public int z;
    public int sourceLevel;
    public int flowRate = 1;
    public int directionX;
    public int directionY;
    public int directionZ;

    public WaterSourceData ToRuntime()
    {
        return new WaterSourceData(
            new Vector3Int(x, y, z),
            sourceLevel,
            flowRate,
            new Vector3Int(directionX, directionY, directionZ));
    }

    public static SavedWaterSource FromRuntime(WaterSourceData source)
    {
        return new SavedWaterSource
        {
            x = source.Position.x,
            y = source.Position.y,
            z = source.Position.z,
            sourceLevel = source.SourceLevel,
            flowRate = source.FlowRate,
            directionX = source.InitialDirection.x,
            directionY = source.InitialDirection.y,
            directionZ = source.InitialDirection.z
        };
    }
}
