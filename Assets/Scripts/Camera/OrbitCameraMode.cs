using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitCameraMode : MonoBehaviour, ICameraMode
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Distance Settings")]
    [SerializeField] private float distance = 30f;
    [SerializeField] private float minDistance = 5f;
    [SerializeField] private float maxDistance = 80f;
    [SerializeField] private float zoomSpeed = 10f;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 50f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Pan Settings")]
    [SerializeField] private float panSpeed = 0.02f;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask;
    [SerializeField] private float collisionPadding = 0.3f;

    private float yaw;
    private float pitch = 45f;

    private Vector2 lastMousePos;

    private bool isRotating;
    private bool isPanning;
    private bool justEntered;

    public bool IsRotating => isRotating;

    private OrbitSnapshot savedState;

    private void Awake()
    {
        if (target == null)
        {
            GameObject pivot =
                new GameObject("Camera Pivot");

            pivot.transform.position =
                Vector3.zero;

            target = pivot.transform;
        }

        Vector3 angles =
            transform.eulerAngles;

        yaw = angles.y;
        pitch = angles.x;
    }

    public void Enter()
    {
        RestoreState();

        VoxelVisibilitySystem.ResetVisibility();

        DwarfVisibilitySystem.ShowAll();

        justEntered = true;

        UpdateCamera();
    }

    public void Exit()
    {
        SaveState();
    }

    public void HandleInput()
    {
        Vector2 mousePos =
            Mouse.current.position.ReadValue();

        if (Mouse.current.rightButton.isPressed)
        {
            if (!isRotating)
            {
                isRotating = true;
                lastMousePos = mousePos;
            }

            Vector2 delta =
                mousePos - lastMousePos;

            yaw +=
                delta.x *
                rotationSpeed *
                Time.deltaTime;

            pitch -=
                delta.y *
                rotationSpeed *
                Time.deltaTime;

            pitch =
                Mathf.Clamp(
                    pitch,
                    minPitch,
                    maxPitch);

            lastMousePos = mousePos;
        }
        else
        {
            isRotating = false;
        }

        if (Mouse.current.middleButton.isPressed)
        {
            if (!isPanning)
            {
                isPanning = true;
                lastMousePos = mousePos;
            }

            Vector2 delta =
                mousePos - lastMousePos;

            Vector3 right =
                transform.right;

            Vector3 up =
                transform.up;

            target.position +=
                (-right * delta.x +
                 -up * delta.y)
                * panSpeed;

            lastMousePos = mousePos;
        }
        else
        {
            isPanning = false;
        }

        float scroll =
            Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            distance -=
                scroll *
                zoomSpeed *
                Time.deltaTime;

            distance =
                Mathf.Clamp(
                    distance,
                    minDistance,
                    maxDistance);
        }
    }

    public void UpdateCamera()
    {
        if (justEntered)
        {
            justEntered = false;
            return;
        }

        Quaternion rotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f);

        Vector3 desiredPosition =
            target.position +
            rotation *
            new Vector3(
                0,
                0,
                -distance);

        Vector3 direction =
            (desiredPosition - target.position)
            .normalized;

        float desiredDistance =
            distance;

        if (Physics.Raycast(
                target.position,
                direction,
                out RaycastHit hit,
                distance,
                collisionMask))
        {
            desiredDistance =
                Mathf.Max(
                    minDistance,
                    hit.distance -
                    collisionPadding);
        }

        Vector3 finalPosition =
            target.position +
            direction * desiredDistance;

        transform.SetPositionAndRotation(
            finalPosition,
            rotation);
    }

    public void SetOrbitCenter(Vector3 center)
    {
        if (target == null)
        {
            GameObject pivot =
                new GameObject("Camera Pivot");

            target = pivot.transform;
        }

        target.position = center;

        // Keep the stored Orbit state synchronized too.
        // Otherwise returning from Puzzle mode could restore
        // the old center after a chunk expansion.
        if (savedState.Distance > 0.01f)
        {
            savedState.TargetPosition = center;
        }
    }

    public void FrameBounds(
        Bounds bounds,
        float padding = 1.15f)
    {
        Camera controlledCamera =
            GetComponent<Camera>();

        if (controlledCamera == null ||
            bounds.size.sqrMagnitude < 0.001f)
        {
            return;
        }

        SetOrbitCenter(bounds.center);

        float radius = bounds.extents.magnitude;
        float verticalHalfAngle =
            controlledCamera.fieldOfView *
            0.5f *
            Mathf.Deg2Rad;

        float horizontalHalfAngle =
            Mathf.Atan(
                Mathf.Tan(verticalHalfAngle) *
                Mathf.Max(0.01f, controlledCamera.aspect));

        float limitingHalfAngle =
            Mathf.Min(verticalHalfAngle, horizontalHalfAngle);

        float requiredDistance =
            radius /
            Mathf.Max(0.01f, Mathf.Sin(limitingHalfAngle));

        distance = Mathf.Max(
            minDistance,
            requiredDistance * Mathf.Max(1f, padding));

        // Generated worlds can legitimately be larger than the old
        // inspector maximum. Preserve zooming room beyond the initial frame.
        maxDistance = Mathf.Max(maxDistance, distance * 1.5f);
    }

    public void SaveState()
    {
        savedState.TargetPosition =
            target.position;

        savedState.Yaw =
            yaw;

        savedState.Pitch =
            pitch;

        savedState.Distance =
            distance;
    }

    public void RestoreState()
    {
        if (savedState.Distance <= 0.01f)
            return;

        target.position =
            savedState.TargetPosition;

        yaw =
            savedState.Yaw;

        pitch =
            savedState.Pitch;

        distance =
            savedState.Distance;
    }

    public Vector3 GetCurrentPosition()
    {
        return transform.position;
    }

    public Quaternion GetCurrentRotation()
    {
        return transform.rotation;
    }

    private struct OrbitSnapshot
    {
        public Vector3 TargetPosition;

        public float Yaw;
        public float Pitch;
        public float Distance;
    }
}
