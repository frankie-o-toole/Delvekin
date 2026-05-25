using UnityEngine;
using UnityEngine.InputSystem;

public class VoxelHover : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private VoxelWorld voxelWorld;
    [SerializeField] private Transform highlight;
    //Color hoverColor;

    private Vector3Int lastVoxel = new(int.MinValue, int.MinValue, int.MinValue);
    void Update()
    {
        UpdateHover();
    }

    private void UpdateHover()
    {
        if (cam == null || voxelWorld == null || highlight == null)
            return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            Vector3 hitPoint = hit.point;

            // Push slightly inside voxel volume
            hitPoint -= hit.normal * 0.01f;

            Vector3Int voxelPos = Vector3Int.FloorToInt(hitPoint);

            // Only update if voxel changed (performance + stability)
            if (voxelPos == lastVoxel)
                return;

            lastVoxel = voxelPos;
            Voxel voxel = voxelWorld.GetVoxel(voxelPos);
            Debug.Log($"Hover voxel: {voxelPos} | Type: {voxel.Type}");
            MoveHighlight(voxelPos);
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
}