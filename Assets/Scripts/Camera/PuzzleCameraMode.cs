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

    private Vector3 cameraPosition;
    private Quaternion cameraRotation;

    private Vector3 panOffset;

    private Vector2 lastMousePos;
    private bool isPanning;

    // -------------------------------------------------
    // Lifecycle
    // -------------------------------------------------

    public void Enter()
    {
        panOffset = Vector3.zero;
        isPanning = false;
    }

    public void Exit()
    {
    }

    public void HandleInput()
    {
        HandlePan();
    }

    public void UpdateCamera()
    {
        Vector3 basePosition = GetPuzzlePosition(currentSide);

        cameraPosition = basePosition + panOffset;
        cameraRotation = GetPuzzleRotation(currentSide);

        Camera.main.transform.position = cameraPosition;
        Camera.main.transform.rotation = cameraRotation;
    }

    // -------------------------------------------------
    // Input: RMB PAN
    // -------------------------------------------------

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

            // Camera-aligned pan directions
            Vector3 right = Camera.main.transform.right;
            Vector3 up = Camera.main.transform.up;

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