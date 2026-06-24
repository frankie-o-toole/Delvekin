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
            Vector3 chunkMin =
                new Vector3(
                    coord.x * chunkSize,
                    coord.y * chunkSize,
                    coord.z * chunkSize);

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
}