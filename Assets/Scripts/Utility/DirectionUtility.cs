using UnityEngine;

public static class DirectionUtility
{
    public static Vector3Int ToVector(PuzzleSide side)
    {
        return side switch
        {
            PuzzleSide.North => Vector3Int.forward,
            PuzzleSide.East => Vector3Int.right,
            PuzzleSide.South => Vector3Int.back,
            PuzzleSide.West => Vector3Int.left,
            _ => Vector3Int.zero,
        };
    }

    public static PuzzleSide Opposite(PuzzleSide side)
    {
        return side switch
        {
            PuzzleSide.North => PuzzleSide.South,
            PuzzleSide.East => PuzzleSide.West,
            PuzzleSide.South => PuzzleSide.North,
            PuzzleSide.West => PuzzleSide.East,
            _ => side,
        };
    }
}