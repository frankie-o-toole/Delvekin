using UnityEngine;
using UnityEngine.InputSystem;

public class VoxelInteraction : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private VoxelWorld voxelWorld;

    private void Update()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryRemoveVoxel();
        }
    }

    private void TryRemoveVoxel()
    {
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            Vector3 hitPoint = hit.point;

            hitPoint -= hit.normal * 0.01f;

            Vector3Int voxelPos = Vector3Int.FloorToInt(hitPoint);

            Debug.Log($"Hit voxel: {voxelPos}");

            voxelWorld.SetVoxel(voxelPos, VoxelType.Air);
        }
    }
}