using UnityEngine;
using UnityEngine.InputSystem;

public class PuzzleCameraMode : MonoBehaviour, ICameraMode
{
    [Header("Level Reference")]
    private Vector3 levelCenter;

    [Header("Puzzle Settings")]
    [SerializeField] private float puzzleDistance = 30f;

    [Header("Pan Settings")]
    [SerializeField] private float panSpeed = 0.02f;

    private PuzzleSide currentSide;

    private Vector3 lockedBasePosition;
    private Quaternion lockedBaseRotation;

    private Vector3 panOffset;

    private Vector2 lastMousePos;
    private bool isPanning;

    // -------------------------------------------------
    // Lifecycle
    // -------------------------------------------------

    public void Enter()
    {
        VoxelVisibilitySystem.SetToInitialPuzzleState();
        DwarfVisibilitySystem.Reset();

        panOffset = Vector3.zero;
        isPanning = false;

        lockedBasePosition = GetPuzzlePosition(currentSide);
        lockedBaseRotation = GetPuzzleRotation(currentSide);
    }

    public void Exit()
    {
    }

    public void HandleInput()
    {
        HandlePan();
        HandleScroll();
    }
    public SliceAxis GetSliceAxis()
    {
        return currentSide == PuzzleSide.East || currentSide == PuzzleSide.West
            ? SliceAxis.X
            : SliceAxis.Z;
    }
    public void UpdateCamera()
    {
        Vector3 basePosition = lockedBasePosition;
        Quaternion baseRotation = lockedBaseRotation;

        transform.SetPositionAndRotation(basePosition + panOffset, baseRotation);
    }

    // -------------------------------------------------
    // Input: RMB PAN
    // -------------------------------------------------

    private void HandleScroll()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            int delta = scroll > 0 ? 1 : -1;

            VoxelVisibilitySystem.ChangeLayer(delta);
            DwarfVisibilitySystem.ChangeLayer(delta);

            ChunkRefreshSystem.RequestFullRefresh();
        }
    }
    private void HandlePan()
    {
        if (Mouse.current == null)
            return;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        if (Mouse.current.rightButton.isPressed)
        {
            if (!isPanning)
            {
                isPanning = true;
                lastMousePos = mousePos;
            }

            Vector2 delta = mousePos - lastMousePos;

            // Use puzzle orientation, NOT Camera.main
            Quaternion rotation = GetPuzzleRotation(currentSide);

            Vector3 right = rotation * Vector3.right;
            Vector3 up = Vector3.up;

            panOffset += (-right * delta.x + -up * delta.y) * panSpeed;

            lastMousePos = mousePos;
        }
        else
        {
            isPanning = false;
        }
    }

    // -------------------------------------------------
    // External setup (DO NOT CHANGE)
    // -------------------------------------------------

    public void SetLevelCenter(Vector3 center)
    {
        levelCenter = center;
    }

    public void SetSide(PuzzleSide side)
    {
        Debug.Log("Current side is " + side);
        currentSide = side;
    }

    // -------------------------------------------------
    // Existing logic (untouched)
    // -------------------------------------------------

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