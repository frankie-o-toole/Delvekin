using UnityEngine;

public static class PuzzleSliceMapping
{
    public static void GetSlice(PuzzleSide side, out SliceAxis axis, out int sign)
    {
        switch (side)
        {
            case PuzzleSide.North:
                axis = SliceAxis.Z;
                sign = +1;
                break;

            case PuzzleSide.South:
                axis = SliceAxis.Z;
                sign = -1;
                break;

            case PuzzleSide.East:
                axis = SliceAxis.X;
                sign = +1;
                break;

            case PuzzleSide.West:
                axis = SliceAxis.X;
                sign = -1;
                break;

            default:
                axis = SliceAxis.Z;
                sign = +1;
                break;
        }
    }
}