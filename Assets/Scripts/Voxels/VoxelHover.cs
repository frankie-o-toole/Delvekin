using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(0)]
public class VoxelHover : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Camera cam;

    [SerializeField]
    private VoxelWorld voxelWorld;

    [SerializeField]
    private Transform highlight;

    [SerializeField]
    private OrbitCameraMode orbitCamera;

    [Header("Raycast")]
    [SerializeField]
    private float maximumRayDistance = 1000f;

    private Vector2 rightMouseStart;
    private bool rightMouseDragged;

    private Vector3Int hoveredVoxel;
    private Vector3Int placementVoxel;

    private bool hasValidVoxelTarget;

    private VoxelType selectedVoxelType =
        VoxelType.Dirt;

    private Vector3Int lastVoxel =
        new(
            int.MinValue,
            int.MinValue,
            int.MinValue);

    private void Update()
    {
        UpdateHover();
        UpdateEditing();
    }

    private bool TryGetMousePosition(
        out Vector2 position)
    {
        position = default;

        if (Mouse.current == null)
        {
            return false;
        }

        position =
            Mouse.current.position.ReadValue();

        if (float.IsNaN(position.x) ||
            float.IsNaN(position.y))
        {
            return false;
        }

        if (cam != null)
        {
            Rect pixelRect =
                cam.pixelRect;

            if (position.x < pixelRect.xMin ||
                position.x > pixelRect.xMax ||
                position.y < pixelRect.yMin ||
                position.y > pixelRect.yMax)
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateHover()
    {
        if (cam == null ||
            voxelWorld == null ||
            highlight == null ||
            IsPointerOverUI() ||
            InteractionState.IsHoveringDwarf)
        {
            ClearHover();
            return;
        }

        if (!TryGetMousePosition(
                out Vector2 mousePosition))
        {
            ClearHover();
            return;
        }

        Ray ray =
            cam.ScreenPointToRay(
                mousePosition);

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                maximumRayDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            ClearHover();
            return;
        }

        // Defensive fallback. DwarfSelectionManager should already
        // have claimed the pointer due to its earlier execution order.
        if (hit.collider.GetComponentInParent<DwarfAgent>() != null)
        {
            ClearHover();
            return;
        }

        Vector3 insidePoint =
            hit.point -
            hit.normal * 0.01f;

        Vector3 outsidePoint =
            hit.point +
            hit.normal * 0.01f;

        hoveredVoxel =
            Vector3Int.FloorToInt(
                insidePoint);

        placementVoxel =
            Vector3Int.FloorToInt(
                outsidePoint);

        hasValidVoxelTarget = true;

        if (hoveredVoxel != lastVoxel)
        {
            lastVoxel =
                hoveredVoxel;

            MoveHighlight(
                hoveredVoxel);
        }

        if (!highlight.gameObject.activeSelf)
        {
            highlight.gameObject.SetActive(true);
        }
    }

    private void ClearHover()
    {
        hasValidVoxelTarget = false;

        hoveredVoxel =
            Vector3Int.zero;

        placementVoxel =
            Vector3Int.zero;

        lastVoxel =
            new Vector3Int(
                int.MinValue,
                int.MinValue,
                int.MinValue);

        if (highlight != null &&
            highlight.gameObject.activeSelf)
        {
            highlight.gameObject.SetActive(false);
        }
    }

    private void MoveHighlight(
        Vector3Int voxelPosition)
    {
        highlight.position =
            new Vector3(
                voxelPosition.x + 0.5f,
                voxelPosition.y + 0.5f,
                voxelPosition.z + 0.5f);
    }

    private void UpdateEditing()
    {
        if (Mouse.current == null)
        {
            return;
        }

        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        if (Mouse.current.rightButton
            .wasPressedThisFrame)
        {
            rightMouseStart =
                mousePosition;

            rightMouseDragged = false;
        }

        if (Mouse.current.rightButton.isPressed &&
            Vector2.Distance(
                mousePosition,
                rightMouseStart) > 5f)
        {
            rightMouseDragged = true;
        }

        if (voxelWorld == null ||
            IsPointerOverUI() ||
            InteractionState.IsHoveringDwarf)
        {
            return;
        }

        UpdateSelectedVoxelType();

        if (!hasValidVoxelTarget)
        {
            return;
        }

        if (Mouse.current.leftButton
            .wasPressedThisFrame)
        {
            voxelWorld.SetVoxel(
                hoveredVoxel,
                VoxelType.Air);
        }

        if (Mouse.current.rightButton
            .wasReleasedThisFrame &&
            !rightMouseDragged)
        {
            voxelWorld.SetVoxel(
                placementVoxel,
                selectedVoxelType);
        }
    }

    private void UpdateSelectedVoxelType()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.digit1Key
            .wasPressedThisFrame)
        {
            SelectVoxelType(
                VoxelType.Dirt,
                "Dirt");
        }

        if (Keyboard.current.digit2Key
            .wasPressedThisFrame)
        {
            SelectVoxelType(
                VoxelType.Granite,
                "Granite");
        }

        if (Keyboard.current.digit3Key
            .wasPressedThisFrame)
        {
            SelectVoxelType(
                VoxelType.Lava,
                "Lava");
        }

        if (Keyboard.current.digit4Key
            .wasPressedThisFrame)
        {
            SelectVoxelType(
                VoxelType.Water,
                "Water");
        }

        if (Keyboard.current.digit5Key
            .wasPressedThisFrame)
        {
            SelectVoxelType(
                VoxelType.Vine,
                "Vine");
        }

        if (Keyboard.current.digit6Key
            .wasPressedThisFrame)
        {
            SelectVoxelType(
                VoxelType.Snow,
                "Snow");
        }

        if (Keyboard.current.digit7Key
            .wasPressedThisFrame)
        {
            SelectVoxelType(
                VoxelType.Bubblegum,
                "Bubblegum");
        }

        if (Keyboard.current.digit8Key
            .wasPressedThisFrame)
        {
            SelectVoxelType(
                VoxelType.SpawnPoint,
                "SpawnPoint");
        }
    }

    private void SelectVoxelType(
        VoxelType type,
        string displayName)
    {
        selectedVoxelType = type;

        Debug.Log(
            $"Selected {displayName}");
    }

    private static bool IsPointerOverUI()
    {
        return
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject();
    }
}