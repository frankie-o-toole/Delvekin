using UnityEngine;

public class PuzzleCameraMode : MonoBehaviour, ICameraMode
{
    [Header("Level Reference")]
    private Vector3 levelCenter;

    [Header("Puzzle Settings")]
    [SerializeField] private float puzzleDistance = 30f;

    private PuzzleSide currentSide;

    public void SetLevelCenter(Vector3 center)
    {
        levelCenter = center;
    }

    public void Enter()
    {
        // Nothing needed yet
    }

    public void Exit()
    {
        // Nothing needed yet
    }

    public void HandleInput()
    {
        // Puzzle mode will handle:
        // - layer scrolling later
        // - panning later
    }

    public void UpdateCamera()
    {
        // Camera is fully controlled by transition system
        // (same pattern as Orbit, good separation)
    }

    // -------------------------------------------------
    // Core API used by CameraStateController
    // -------------------------------------------------

    public void SetSide(PuzzleSide side)
    {
        currentSide = side;
    }

    public Vector3 GetPuzzlePosition(PuzzleSide side)
    {
        switch (side)
        {
            case PuzzleSide.North:
                return levelCenter + Vector3.forward * puzzleDistance;

            case PuzzleSide.South:
                return levelCenter + Vector3.back * puzzleDistance;

            case PuzzleSide.East:
                return levelCenter + Vector3.right * puzzleDistance;

            case PuzzleSide.West:
                return levelCenter + Vector3.left * puzzleDistance;

            default:
                return levelCenter;
        }
    }

    public Quaternion GetPuzzleRotation(PuzzleSide side)
    {
        Vector3 position = GetPuzzlePosition(side);

        return Quaternion.LookRotation(
            levelCenter - position,
            Vector3.up);
    }
}