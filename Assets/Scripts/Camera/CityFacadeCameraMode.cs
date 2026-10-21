using UnityEngine;
using UnityEngine.InputSystem;

public sealed class CityFacadeCameraMode : MonoBehaviour, ICameraMode
{
    [Header("Authored Camera Pose")]
    [Tooltip(
        "Place this transform at the desired default city camera pose.")]
    [SerializeField]
    private Transform cameraAnchor;

    [Header("Pan")]
    [SerializeField]
    private float panSpeed = 0.02f;

    [Tooltip("Maximum horizontal and vertical pan from the anchor.")]
    [SerializeField]
    private Vector2 panLimits = new(20f, 12f);

    [Header("Zoom")]
    [SerializeField]
    private float zoomSpeed = 2f;

    [SerializeField]
    private float minimumZoomOffset = -15f;

    [SerializeField]
    private float maximumZoomOffset = 15f;

    [SerializeField]
    private float maximumZoomChangePerFrame = 4f;

    private Vector2 panOffset;
    private float zoomOffset;
    private Vector2 lastPanMousePosition;
    private bool isPanning;

    public void Enter()
    {
        isPanning = false;
        UpdateCamera();
    }

    public void Exit()
    {
        isPanning = false;
    }

    public void ResetView()
    {
        panOffset = Vector2.zero;
        zoomOffset = 0f;
        isPanning = false;
    }

    public void HandleInput()
    {
        HandlePan();
        HandleZoom();
    }

    public void UpdateCamera()
    {
        CameraPose pose = GetCurrentPose();
        transform.SetPositionAndRotation(
            pose.Position,
            pose.Rotation);
    }

    public CameraPose GetCurrentPose()
    {
        if (cameraAnchor == null)
        {
            return new CameraPose(
                transform.position,
                transform.rotation);
        }

        Vector3 position =
            cameraAnchor.position +
            cameraAnchor.right * panOffset.x +
            cameraAnchor.up * panOffset.y +
            cameraAnchor.forward * zoomOffset;

        return new CameraPose(
            position,
            cameraAnchor.rotation);
    }

    private void HandlePan()
    {
        if (Mouse.current == null)
        {
            return;
        }

        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        if (Mouse.current.rightButton.isPressed)
        {
            if (!isPanning)
            {
                isPanning = true;
                lastPanMousePosition = mousePosition;
                return;
            }

            Vector2 delta =
                mousePosition - lastPanMousePosition;

            panOffset += new Vector2(
                -delta.x * panSpeed,
                -delta.y * panSpeed);
            panOffset.x = Mathf.Clamp(
                panOffset.x,
                -Mathf.Abs(panLimits.x),
                Mathf.Abs(panLimits.x));
            panOffset.y = Mathf.Clamp(
                panOffset.y,
                -Mathf.Abs(panLimits.y),
                Mathf.Abs(panLimits.y));

            lastPanMousePosition = mousePosition;
        }
        else
        {
            isPanning = false;
        }
    }

    private void HandleZoom()
    {
        if (Mouse.current == null)
        {
            return;
        }

        float rawScroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(rawScroll) < 0.01f)
        {
            return;
        }

        float scrollSteps = Mathf.Abs(rawScroll) >= 10f
            ? rawScroll / 120f
            : rawScroll;
        float change = Mathf.Clamp(
            scrollSteps * zoomSpeed,
            -maximumZoomChangePerFrame,
            maximumZoomChangePerFrame);

        zoomOffset = Mathf.Clamp(
            zoomOffset + change,
            minimumZoomOffset,
            maximumZoomOffset);
    }

    private void OnValidate()
    {
        panSpeed = Mathf.Max(0f, panSpeed);
        panLimits.x = Mathf.Max(0f, panLimits.x);
        panLimits.y = Mathf.Max(0f, panLimits.y);
        zoomSpeed = Mathf.Max(0f, zoomSpeed);
        maximumZoomChangePerFrame =
            Mathf.Max(0f, maximumZoomChangePerFrame);

        if (maximumZoomOffset < minimumZoomOffset)
        {
            maximumZoomOffset = minimumZoomOffset;
        }
    }
}
