using UnityEngine;
using UnityEngine.InputSystem;

public class VoxelHover : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private VoxelWorld voxelWorld;
    [SerializeField] private Transform highlight;
    [SerializeField] private OrbitCameraMode orbitCamera;

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
        out Vector2 pos)
    {
        pos = default;

        if (Mouse.current == null)
            return false;

        pos =
            Mouse.current.position.ReadValue();

        if (
            float.IsNaN(pos.x) ||
            float.IsNaN(pos.y))
        {
            return false;
        }

        // Don't raycast if the mouse is outside
        // this camera's rendered viewport.
        if (cam != null)
        {
            Rect pixelRect =
                cam.pixelRect;

            if (
                pos.x < pixelRect.xMin ||
                pos.x > pixelRect.xMax ||
                pos.y < pixelRect.yMin ||
                pos.y > pixelRect.yMax)
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateHover()
    {
        if (
            cam == null ||
            voxelWorld == null ||
            highlight == null)
        {
            ClearHover();
            return;
        }

        if (InteractionState.IsHoveringDwarf)
        {
            ClearHover();
            return;
        }

        if (
            !TryGetMousePosition(
                out Vector2 mousePos))
        {
            ClearHover();
            return;
        }

        Ray ray =
            cam.ScreenPointToRay(mousePos);

        if (
            Physics.Raycast(
                ray,
                out RaycastHit hit,
                100f))
        {
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

            if (
                !highlight.gameObject.activeSelf)
            {
                highlight.gameObject.SetActive(
                    true);
            }

            return;
        }

        ClearHover();
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

        if (
            highlight != null &&
            highlight.gameObject.activeSelf)
        {
            highlight.gameObject.SetActive(
                false);
        }
    }

    private void MoveHighlight(
        Vector3Int voxelPos)
    {
        Vector3 worldPos =
            new(
                voxelPos.x + 0.5f,
                voxelPos.y + 0.5f,
                voxelPos.z + 0.5f);

        highlight.position =
            worldPos;
    }

    private void UpdateEditing()
    {
        if (Mouse.current == null)
            return;

        Vector2 mousePos =
            Mouse.current.position.ReadValue();

        if (
            Mouse.current.rightButton
                .wasPressedThisFrame)
        {
            rightMouseStart =
                mousePos;

            rightMouseDragged =
                false;
        }

        if (
            Mouse.current.rightButton.isPressed)
        {
            if (
                Vector2.Distance(
                    mousePos,
                    rightMouseStart) > 5f)
            {
                rightMouseDragged = true;
            }
        }

        if (voxelWorld == null)
            return;

        if (InteractionState.IsHoveringDwarf)
            return;

        if (
            Keyboard.current.digit1Key
                .wasPressedThisFrame)
        {
            selectedVoxelType =
                VoxelType.Dirt;

            Debug.Log(
                "Selected Dirt");
        }

        if (
            Keyboard.current.digit2Key
                .wasPressedThisFrame)
        {
            selectedVoxelType =
                VoxelType.Granite;

            Debug.Log(
                "Selected Granite");
        }

        if (
            Keyboard.current.digit3Key
                .wasPressedThisFrame)
        {
            selectedVoxelType =
                VoxelType.Lava;

            Debug.Log(
                "Selected Lava");
        }

        if (
            Keyboard.current.digit4Key
                .wasPressedThisFrame)
        {
            selectedVoxelType =
                VoxelType.Water;

            Debug.Log(
                "Selected Water");
        }

        if (
            Keyboard.current.digit5Key
                .wasPressedThisFrame)
        {
            selectedVoxelType =
                VoxelType.Vine;

            Debug.Log(
                "Selected Vine");
        }

        if (
            Keyboard.current.digit6Key
                .wasPressedThisFrame)
        {
            selectedVoxelType =
                VoxelType.Snow;

            Debug.Log(
                "Selected Snow");
        }

        if (
            Keyboard.current.digit7Key
                .wasPressedThisFrame)
        {
            selectedVoxelType =
                VoxelType.Bubblegum;

            Debug.Log(
                "Selected Bubblegum");
        }

        if (
            Keyboard.current.digit8Key
                .wasPressedThisFrame)
        {
            selectedVoxelType =
                VoxelType.SpawnPoint;

            Debug.Log(
                "Selected SpawnPoint");
        }

        // -----------------------------------------
        // EDITING REQUIRES A VALID VOXEL TARGET
        // -----------------------------------------

        if (!hasValidVoxelTarget)
            return;

        // LMB = REMOVE
        if (
            Mouse.current.leftButton
                .wasPressedThisFrame)
        {
            voxelWorld.SetVoxel(
                hoveredVoxel,
                VoxelType.Air);
        }

        // RMB = PLACE
        if (
            Mouse.current.rightButton
                .wasReleasedThisFrame)
        {
            if (!rightMouseDragged)
            {
                voxelWorld.SetVoxel(
                    placementVoxel,
                    selectedVoxelType);
            }
        }
    }
}