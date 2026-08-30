using UnityEngine;

public static class VoxelVisibilitySystem
{
    private static SliceAxis axis;
    private static int directionSign;

    private static int minX;
    private static int maxX;

    private static int minZ;
    private static int maxZ;

    // Kept public for compatibility/debugging.
    public static int minLayer;
    public static int maxLayer;

    private static int peelDepth;

    public static void SetView(SliceAxis newAxis, int sign)
    {
        axis = newAxis;
        directionSign = sign;

        ApplyActiveAxisBounds();

        peelDepth = Mathf.Clamp(
            peelDepth,
            0,
            maxLayer - minLayer
        );
    }

    public static void SetBounds(
        int newMinX,
        int newMaxX,
        int newMinZ,
        int newMaxZ)
    {
        minX = newMinX;
        maxX = newMaxX;

        minZ = newMinZ;
        maxZ = newMaxZ;

        ApplyActiveAxisBounds();

        peelDepth = Mathf.Clamp(
            peelDepth,
            0,
            maxLayer - minLayer
        );
    }

    // Compatibility overload.
    // Can be removed later if nothing else uses the old API.
    public static void SetBounds(int min, int max)
    {
        SetBounds(min, max, min, max);
    }

    public static void SetToInitialPuzzleState()
    {
        peelDepth = 0;
    }

    public static bool ChangeLayer(
        int delta,
        out int oldVisibleBoundary,
        out int newVisibleBoundary)
    {
        oldVisibleBoundary =
            GetVisibleBoundary();

        int oldPeelDepth = peelDepth;

        peelDepth = Mathf.Clamp(
            peelDepth + delta,
            0,
            maxLayer - minLayer
        );

        newVisibleBoundary =
            GetVisibleBoundary();

        return peelDepth != oldPeelDepth;
    }

    private static int GetVisibleBoundary()
    {
        return directionSign > 0
            ? maxLayer - peelDepth
            : minLayer + peelDepth;
    }

    public static bool IsVoxelVisible(Vector3Int worldPos)
    {
        int coord = GetAxisCoordinate(worldPos);

        int start =
            directionSign > 0
                ? maxLayer
                : minLayer;

        int distanceFromFront =
            Mathf.Abs(coord - start);

        return distanceFromFront >= peelDepth;
    }

    private static int GetAxisCoordinate(Vector3Int pos)
    {
        return axis switch
        {
            SliceAxis.X => pos.x,
            SliceAxis.Z => pos.z,
            _ => pos.x
        };
    }

    private static void ApplyActiveAxisBounds()
    {
        switch (axis)
        {
            case SliceAxis.X:
                minLayer = minX;
                maxLayer = maxX;
                break;

            case SliceAxis.Z:
                minLayer = minZ;
                maxLayer = maxZ;
                break;

            default:
                minLayer = minX;
                maxLayer = maxX;
                break;
        }
    }

    public static void ResetVisibility()
    {
        peelDepth = 0;
    }
}
