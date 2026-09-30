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

    [Header("Raycast")]
    [SerializeField]
    private float maximumRayDistance = 1000f;

    private Vector3Int lastVoxel =
        new(
            int.MinValue,
            int.MinValue,
            int.MinValue);

    private void Update()
    {
        UpdateHover();
    }

    private void UpdateHover()
    {
        if (cam == null ||
            voxelWorld == null ||
            highlight == null ||
            IsPointerOverUI() ||
            InteractionState.IsHoveringDwarf ||
            !TryGetMousePosition(out Vector2 mousePosition))
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

        // DwarfSelectionManager has an earlier execution order and owns
        // dwarf interaction. This fallback prevents the terrain highlight
        // from appearing through a dwarf collider.
        if (hit.collider.GetComponentInParent<DwarfAgent>() != null)
        {
            ClearHover();
            return;
        }

        Vector3 insidePoint =
            hit.point -
            hit.normal * 0.01f;

        Vector3Int hoveredVoxel =
            Vector3Int.FloorToInt(
                insidePoint);

        if (hoveredVoxel != lastVoxel)
        {
            lastVoxel = hoveredVoxel;

            highlight.position =
                new Vector3(
                    hoveredVoxel.x + 0.5f,
                    hoveredVoxel.y + 0.5f,
                    hoveredVoxel.z + 0.5f);
        }

        if (!highlight.gameObject.activeSelf)
        {
            highlight.gameObject.SetActive(true);
        }
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

        if (cam == null)
        {
            return true;
        }

        Rect pixelRect =
            cam.pixelRect;

        return
            position.x >= pixelRect.xMin &&
            position.x <= pixelRect.xMax &&
            position.y >= pixelRect.yMin &&
            position.y <= pixelRect.yMax;
    }

    private void ClearHover()
    {
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

    private static bool IsPointerOverUI()
    {
        return
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject();
    }
}
