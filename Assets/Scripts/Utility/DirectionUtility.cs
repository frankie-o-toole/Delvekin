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

    public static PuzzleSide ApplyTurn(
        PuzzleSide approachDirection,
        DirectionAltererTurn turn)
    {
        return turn switch
        {
            DirectionAltererTurn.Left =>
                approachDirection switch
                {
                    PuzzleSide.North => PuzzleSide.West,
                    PuzzleSide.West => PuzzleSide.South,
                    PuzzleSide.South => PuzzleSide.East,
                    PuzzleSide.East => PuzzleSide.North,
                    _ => approachDirection
                },

            DirectionAltererTurn.Right =>
                approachDirection switch
                {
                    PuzzleSide.North => PuzzleSide.East,
                    PuzzleSide.East => PuzzleSide.South,
                    PuzzleSide.South => PuzzleSide.West,
                    PuzzleSide.West => PuzzleSide.North,
                    _ => approachDirection
                },

            _ => Opposite(approachDirection)
        };
    }
}
