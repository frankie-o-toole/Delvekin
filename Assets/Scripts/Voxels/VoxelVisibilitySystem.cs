using UnityEngine;

public static class VoxelVisibilitySystem
{
    private static SliceAxis axis;
    private static int directionSign;

    public static int minLayer;
    public static int maxLayer;

    private static int peelDepth; // <-- IMPORTANT: how many layers are hidden from front

    public static void SetView(SliceAxis newAxis, int sign)
    {
        axis = newAxis;
        directionSign = sign;
    }

    public static void SetBounds(int min, int max)
    {
        minLayer = min;
        maxLayer = max;
    }

    public static void SetToInitialPuzzleState()
    {
        peelDepth = 0;
    }

    public static void ChangeLayer(int delta)
    {
        peelDepth = Mathf.Clamp(
            peelDepth + delta,
            0,
            maxLayer - minLayer
        );
    }

    public static bool IsVoxelVisible(Vector3Int worldPos)
    {
        int coord = GetAxisCoordinate(worldPos);

        int start = directionSign > 0 ? maxLayer : minLayer;

        int distanceFromFront = Mathf.Abs(coord - start);

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

    public static void ResetVisibility()
    {
        peelDepth = 0;
    }
}