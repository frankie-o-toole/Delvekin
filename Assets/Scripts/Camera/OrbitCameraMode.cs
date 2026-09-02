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

    [Tooltip("Maximum distance change accepted from one rendered frame.")]
    [SerializeField] private float maximumZoomChangePerFrame = 5f;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 50f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Tooltip("Prevents focus changes or cursor warps from causing a huge rotation in one frame.")]
    [SerializeField] private float maximumRotationChangePerFrame = 15f;

    [Header("Pan Settings")]
    [SerializeField] private float panSpeed = 0.02f;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask;
    [SerializeField] private float collisionPadding = 0.3f;

    private float yaw;
    private float pitch = 45f;

    private Vector2 lastRotationMousePos;
    private Vector2 lastPanMousePos;

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
        if (Mouse.current == null)
        {
            return;
        }

        Vector2 mousePos =
            Mouse.current.position.ReadValue();

        if (Mouse.current.rightButton.isPressed)
        {
            if (!isRotating)
            {
                isRotating = true;
                lastRotationMousePos = mousePos;
            }

            Vector2 delta =
                mousePos - lastRotationMousePos;

            float yawChange =
                Mathf.Clamp(
                    delta.x *
                    rotationSpeed *
                    Time.deltaTime,
                    -maximumRotationChangePerFrame,
                    maximumRotationChangePerFrame);

            float pitchChange =
                Mathf.Clamp(
                    delta.y *
                    rotationSpeed *
                    Time.deltaTime,
                    -maximumRotationChangePerFrame,
                    maximumRotationChangePerFrame);

            yaw +=
                yawChange;

            pitch -=
                pitchChange;

            pitch =
                Mathf.Clamp(
                    pitch,
                    minPitch,
                    maxPitch);

            lastRotationMousePos = mousePos;
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
                lastPanMousePos = mousePos;
            }

            Vector2 delta =
                mousePos - lastPanMousePos;

            Vector3 right =
                transform.right;

            Vector3 up =
                transform.up;

            target.position +=
                (-right * delta.x +
                 -up * delta.y)
                * panSpeed;

            lastPanMousePos = mousePos;
        }
        else
        {
            isPanning = false;
        }

        float scroll =
            Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            float scrollSteps =
                NormalizeScrollSteps(
                    scroll);

            float zoomChange =
                Mathf.Clamp(
                    scrollSteps * zoomSpeed,
                    -maximumZoomChangePerFrame,
                    maximumZoomChangePerFrame);

            distance -=
                zoomChange;

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

        /*
         * Cast from the desired camera position back toward the orbit
         * target. The target commonly sits inside the voxel level. Casting
         * outward from that point could hit terrain immediately and collapse
         * the camera to minDistance when the orbit angle changed.
         */
        if (Physics.Raycast(
                desiredPosition,
                -direction,
                out RaycastHit hit,
                distance,
                collisionMask))
        {
            desiredDistance =
                Mathf.Clamp(
                    distance -
                    hit.distance +
                    collisionPadding,
                    minDistance,
                    distance);
        }

        Vector3 finalPosition =
            target.position +
            direction * desiredDistance;

        transform.SetPositionAndRotation(
            finalPosition,
            rotation);
    }

    private static float NormalizeScrollSteps(
        float rawScroll)
    {
        /*
         * Windows commonly reports one wheel notch as 120 while some mice
         * and platforms report values close to 1. Convert both conventions
         * into a small, predictable step count.
         */
        float steps =
            Mathf.Abs(rawScroll) > 10f
                ? rawScroll / 120f
                : rawScroll;

        return Mathf.Clamp(
            steps,
            -3f,
            3f);
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