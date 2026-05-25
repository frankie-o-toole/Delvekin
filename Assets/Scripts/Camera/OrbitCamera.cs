using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Distance Settings")]
    [SerializeField] private float distance = 25f;
    [SerializeField] private float minDistance = 5f;
    [SerializeField] private float maxDistance = 80f;
    [SerializeField] private float zoomSpeed = 10f;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 180f;
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

    private void Start()
    {
        if (target == null)
        {
            GameObject pivot = new GameObject("Camera Pivot");
            pivot.transform.position = Vector3.zero;
            target = pivot.transform;
        }

        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    private void Update()
    {
        HandleInput();
    }

    private void LateUpdate()
    {
        UpdateCamera();
    }

    private void HandleInput()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();

        // RIGHT MOUSE = ROTATE
        if (Mouse.current.rightButton.isPressed)
        {
            if (!isRotating)
            {
                isRotating = true;
                lastMousePos = mousePos;
            }

            Vector2 delta = mousePos - lastMousePos;

            yaw += delta.x * rotationSpeed * Time.deltaTime;
            pitch -= delta.y * rotationSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            lastMousePos = mousePos;
        }
        else
        {
            isRotating = false;
        }

        // MIDDLE MOUSE = PAN
        if (Mouse.current.middleButton.isPressed)
        {
            if (!isPanning)
            {
                isPanning = true;
                lastMousePos = mousePos;
            }

            Vector2 delta = mousePos - lastMousePos;

            Vector3 right = transform.right;
            Vector3 up = transform.up;

            target.position += (-right * delta.x + -up * delta.y) * panSpeed;

            lastMousePos = mousePos;
        }
        else
        {
            isPanning = false;
        }

        // SCROLL = ZOOM
        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            distance -= scroll * zoomSpeed * Time.deltaTime;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }
    }

    private void UpdateCamera()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 desiredPosition = target.position + rotation * new Vector3(0, 0, -distance);

        // VOXEL-SAFE COLLISION
        Vector3 direction = (desiredPosition - target.position).normalized;
        float desiredDistance = distance;

        if (Physics.Raycast(target.position, direction, out RaycastHit hit, distance, collisionMask))
        {
            desiredDistance = Mathf.Max(minDistance, hit.distance - collisionPadding);
        }

        Vector3 finalPosition = target.position + direction * desiredDistance;

        transform.position = finalPosition;
        transform.rotation = rotation;
    }
}