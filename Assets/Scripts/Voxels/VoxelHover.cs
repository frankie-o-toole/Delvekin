using UnityEngine;
using UnityEngine.InputSystem;

public class VoxelHover : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private VoxelWorld voxelWorld;
    [SerializeField] private Transform highlight;

    private Vector3Int hoveredVoxel;
    private Vector3Int placementVoxel;

    private VoxelType selectedVoxelType = VoxelType.Dirt;

    private Vector3Int lastVoxel = new(int.MinValue, int.MinValue, int.MinValue);
    void Update()
    {
        UpdateHover();
        UpdateEditing();
    }

    private bool TryGetMousePosition(out Vector2 pos)
    {
        pos = default;

        if (Mouse.current == null)
            return false;

        pos = Mouse.current.position.ReadValue();

        if (float.IsNaN(pos.x) || float.IsNaN(pos.y))
            return false;

        return true;
    }
    private void UpdateHover()
    {
        if (cam == null || voxelWorld == null || highlight == null)
            return;

        if (!TryGetMousePosition(out Vector2 mousePos))
            return;

        Ray ray = cam.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            Vector3 insidePoint = hit.point - hit.normal * 0.01f;
            Vector3 outsidePoint = hit.point + hit.normal * 0.01f;

            hoveredVoxel = Vector3Int.FloorToInt(insidePoint);
            placementVoxel = Vector3Int.FloorToInt(outsidePoint);

            if (hoveredVoxel == lastVoxel)
                return;

            lastVoxel = hoveredVoxel;

            MoveHighlight(hoveredVoxel);
        }
    }

    private void MoveHighlight(Vector3Int voxelPos)
    {
        // Convert voxel coord to world position
        Vector3 worldPos = new(
            voxelPos.x + 0.5f,
            voxelPos.y + 0.5f,
            voxelPos.z + 0.5f
        );

        highlight.position = worldPos;
    }

    private void UpdateEditing()
    {
        if (voxelWorld == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            selectedVoxelType = VoxelType.Dirt;
            Debug.Log("Selected Dirt");
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            selectedVoxelType = VoxelType.Granite;
            Debug.Log("Selected Granite");
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            voxelWorld.SetVoxel(
                hoveredVoxel,
                VoxelType.Air);
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            voxelWorld.SetVoxel(
                placementVoxel,
                selectedVoxelType);
        }
    }
}