using UnityEngine;
using UnityEngine.InputSystem;

public class PuzzleCameraMode :
    MonoBehaviour,
    ICameraMode
{
    [Header("Occupied Level Bounds")]
    private Bounds occupiedBounds;

    [Header("Puzzle Distance")]
    [Tooltip(
        "Distance from the closest occupied voxel surface " +
        "when first entering Puzzle mode.")]
    [SerializeField]
    private float startDistance = 40f;

    [Tooltip(
        "Closest the camera may move toward the nearest " +
        "occupied voxel surface.")]
    [SerializeField]
    private float minimumDistance = 20f;

    [Tooltip(
        "Furthest the camera may move from the nearest " +
        "occupied voxel surface.")]
    [SerializeField]
    private float maximumDistance = 80f;

    [Header("Zoom Settings")]
    [Tooltip(
        "How quickly vertical middle-mouse movement " +
        "changes camera distance.")]
    [SerializeField]
    private float zoomSpeed = 0.1f;

    [Tooltip(
        "Invert middle-mouse vertical zoom direction.")]
    [SerializeField]
    private bool invertZoom = false;

    [Header("Pan Settings")]
    [SerializeField]
    private float panSpeed = 0.02f;

    [Tooltip(
        "Invert horizontal right-mouse panning.")]
    [SerializeField]
    private bool invertPanX = false;

    [Tooltip(
        "Invert vertical right-mouse panning.")]
    [SerializeField]
    private bool invertPanY = false;

    private PuzzleSide currentSide;

    private Vector3 lockedBasePosition;
    private Quaternion lockedBaseRotation;

    private Vector3 panOffset;

    private float currentDistance;

    private Vector2 lastPanMousePos;
    private Vector2 lastZoomMousePos;

    private bool isPanning;
    private bool isZooming;

    // =====================================================
    // LIFECYCLE
    // =====================================================

    public void Enter()
    {
        VoxelVisibilitySystem
            .SetToInitialPuzzleState();

        DwarfVisibilitySystem.Reset();

        panOffset =
            Vector3.zero;

        isPanning =
            false;

        isZooming =
            false;

        currentDistance =
            Mathf.Clamp(
                startDistance,
                minimumDistance,
                maximumDistance);

        UpdateLockedCameraState();
    }

    public void Exit()
    {
        isPanning =
            false;

        isZooming =
            false;
    }

    public void HandleInput()
    {
        HandlePan();
        HandleZoom();
        HandleScroll();
    }

    public void UpdateCamera()
    {
        transform.SetPositionAndRotation(
            lockedBasePosition +
            panOffset,
            lockedBaseRotation);
    }

    // =====================================================
    // SLICE
    // =====================================================

    public SliceAxis GetSliceAxis()
    {
        return
            currentSide ==
                PuzzleSide.East ||
            currentSide ==
                PuzzleSide.West
                ? SliceAxis.X
                : SliceAxis.Z;
    }

    private void HandleScroll()
    {
        if (Mouse.current == null)
            return;

        float scroll =
            Mouse.current.scroll
                .ReadValue().y;

        if (
            Mathf.Abs(scroll) <
            0.01f)
        {
            return;
        }

        int delta =
            scroll > 0
                ? 1
                : -1;

        VoxelVisibilitySystem
            .ChangeLayer(delta);

        DwarfVisibilitySystem
            .ChangeLayer(delta);

        ChunkRefreshSystem
            .RequestFullRefresh();
    }

    // =====================================================
    // PAN - RMB
    // =====================================================

    private void HandlePan()
    {
        if (Mouse.current == null)
            return;

        Vector2 mousePos =
            Mouse.current.position
                .ReadValue();

        if (
            Mouse.current.rightButton
                .isPressed)
        {
            if (!isPanning)
            {
                isPanning =
                    true;

                lastPanMousePos =
                    mousePos;
            }

            Vector2 delta =
                mousePos -
                lastPanMousePos;

            Vector3 right =
                lockedBaseRotation * Vector3.right;

            Vector3 up =
                Vector3.up;

            float horizontalDirection =
                invertPanX
                    ? 1f
                    : -1f;

            float verticalDirection =
                invertPanY
                    ? 1f
                    : -1f;

            panOffset +=
                (
                    right *
                    delta.x *
                    horizontalDirection
                    +
                    up *
                    delta.y *
                    verticalDirection
                )
                *
                panSpeed;

            lastPanMousePos =
                mousePos;
        }
        else
        {
            isPanning =
                false;
        }
    }

    // =====================================================
    // ZOOM - MMB
    // =====================================================

    private void HandleZoom()
    {
        if (Mouse.current == null)
            return;

        Vector2 mousePos =
            Mouse.current.position
                .ReadValue();

        if (
            Mouse.current.middleButton
                .isPressed)
        {
            if (!isZooming)
            {
                isZooming =
                    true;

                lastZoomMousePos =
                    mousePos;

                return;
            }

            Vector2 delta =
                mousePos -
                lastZoomMousePos;

            // Default behaviour:
            //
            // Mouse upward:
            // zoom in.
            //
            // Mouse downward:
            // zoom out.
            float direction =
                invertZoom
                    ? 1f
                    : -1f;

            currentDistance +=
                delta.y *
                zoomSpeed *
                direction;

            currentDistance =
                Mathf.Clamp(
                    currentDistance,
                    minimumDistance,
                    maximumDistance);

            UpdateLockedCameraState();

            lastZoomMousePos =
                mousePos;
        }
        else
        {
            isZooming =
                false;
        }
    }

    // =====================================================
    // LEVEL DATA
    // =====================================================

    public void SetOccupiedBounds(
        Bounds bounds)
    {
        occupiedBounds =
            bounds;

        UpdateLockedCameraState();
    }

    public void SetSide(
        PuzzleSide side)
    {
        currentSide =
            side;

        Debug.Log(
            "Current side is " +
            side);

        currentDistance =
            Mathf.Clamp(
                startDistance,
                minimumDistance,
                maximumDistance);

        UpdateLockedCameraState();
    }

    // =====================================================
    // CAMERA POSITION
    // =====================================================

    private void UpdateLockedCameraState()
    {
        lockedBasePosition =
            GetPuzzlePosition(
                currentSide);

        lockedBaseRotation =
            GetPuzzleRotation(
                currentSide);
    }

    public Vector3 GetPuzzlePosition(
        PuzzleSide side)
    {
        Vector3 position =
            occupiedBounds.center;

        switch (side)
        {
            // Positive Z side.
            //
            // occupiedBounds.max.z is the actual
            // surface of the closest occupied voxel.
            case PuzzleSide.North:

                position.z =
                    occupiedBounds.max.z +
                    currentDistance;

                break;

            // Negative Z side.
            case PuzzleSide.South:

                position.z =
                    occupiedBounds.min.z -
                    currentDistance;

                break;

            // Positive X side.
            case PuzzleSide.East:

                position.x =
                    occupiedBounds.max.x +
                    currentDistance;

                break;

            // Negative X side.
            case PuzzleSide.West:

                position.x =
                    occupiedBounds.min.x -
                    currentDistance;

                break;
        }

        return position;
    }

    public Quaternion GetPuzzleRotation(
        PuzzleSide side)
    {
        Vector3 direction =
            side switch
            {
                PuzzleSide.North =>
                    Vector3.back,

                PuzzleSide.South =>
                    Vector3.forward,

                PuzzleSide.East =>
                    Vector3.left,

                PuzzleSide.West =>
                    Vector3.right,

                _ =>
                    Vector3.back
            };

        return Quaternion.LookRotation(
            direction,
            Vector3.up);
    }

    // =====================================================
    // FUTURE SETTINGS API
    // =====================================================

    public void SetInvertZoom(
        bool inverted)
    {
        invertZoom =
            inverted;
    }

    public void SetInvertPanX(
        bool inverted)
    {
        invertPanX =
            inverted;
    }

    public void SetInvertPanY(
        bool inverted)
    {
        invertPanY =
            inverted;
    }
}