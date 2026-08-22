using System.Collections.Generic;
using UnityEngine;

public static class LevelBoundsUtility
{
    public static Vector3 CalculateCenter(
        IEnumerable<Vector3Int> chunkCoords,
        int chunkSize)
    {
        bool first = true;

        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        foreach (Vector3Int coord in chunkCoords)
        {
            Vector3 chunkMin = new(
                coord.x * chunkSize,
                coord.y * chunkSize,
                coord.z * chunkSize
            );

            Vector3 chunkMax =
                chunkMin + Vector3.one * chunkSize;

            if (first)
            {
                min = chunkMin;
                max = chunkMax;
                first = false;
            }
            else
            {
                min = Vector3.Min(min, chunkMin);
                max = Vector3.Max(max, chunkMax);
            }
        }

        if (first)
            return Vector3.zero;

        return (min + max) * 0.5f;
    }

    public static bool TryCalculateHorizontalVoxelBounds(
        IEnumerable<Vector3Int> chunkCoords,
        int chunkSize,
        out int minX,
        out int maxX,
        out int minZ,
        out int maxZ)
    {
        bool first = true;

        minX = 0;
        maxX = 0;
        minZ = 0;
        maxZ = 0;

        foreach (Vector3Int coord in chunkCoords)
        {
            int chunkMinX = coord.x * chunkSize;
            int chunkMaxX = chunkMinX + chunkSize - 1;

            int chunkMinZ = coord.z * chunkSize;
            int chunkMaxZ = chunkMinZ + chunkSize - 1;

            if (first)
            {
                minX = chunkMinX;
                maxX = chunkMaxX;

                minZ = chunkMinZ;
                maxZ = chunkMaxZ;

                first = false;
            }
            else
            {
                minX = Mathf.Min(minX, chunkMinX);
                maxX = Mathf.Max(maxX, chunkMaxX);

                minZ = Mathf.Min(minZ, chunkMinZ);
                maxZ = Mathf.Max(maxZ, chunkMaxZ);
            }
        }

        return !first;
    }
}