using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(0)]
public class VoxelHover : MonoBehaviour
{
    private enum EditorShape
    {
        Single,
        Line,
        Box
    }

    private enum EditorAction
    {
        Place,
        Erase
    }

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

    [Header("Editor Tools")]
    [SerializeField]
    private int maximumVoxelsPerOperation = 65536;

    [SerializeField]
    private int maximumUndoSteps = 20;

    private EditorShape selectedShape = EditorShape.Single;
    private EditorAction selectedAction = EditorAction.Place;

    private bool isDraggingTool;
    private Vector3Int dragStartVoxel;

    private readonly Stack<Dictionary<Vector3Int, Voxel>> undoHistory =
        new();

    private readonly List<Transform> linePreviewMarkers =
        new();

    private Transform boxPreviewMarker;
    private Vector3 highlightBaseScale = Vector3.one;

    private const float EditorUiScale = 2.5f;
    private const float EditorPanelWidth = 190f;
    private const float EditorPanelHeight = 155f;
    private const float EditorPanelMargin = 10f;

    private void Awake()
    {
        if (highlight != null)
        {
            highlightBaseScale = highlight.localScale;
        }
    }

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
        UpdateToolPreview();
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
            IsPointerOverEditorControls() ||
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
        if (Keyboard.current != null &&
            Keyboard.current.zKey.wasPressedThisFrame &&
            (Keyboard.current.leftCtrlKey.isPressed ||
             Keyboard.current.rightCtrlKey.isPressed))
        {
            UndoLastOperation();
            return;
        }

        if (Mouse.current == null)
        {
            return;
        }

        if (voxelWorld == null ||
            IsPointerOverUI() ||
            IsPointerOverEditorControls() ||
            InteractionState.IsHoveringDwarf)
        {
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                isDraggingTool = false;
                ClearToolPreview();
            }

            return;
        }

        UpdateSelectedVoxelType();

        if (!hasValidVoxelTarget)
        {
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                isDraggingTool = false;
                ClearToolPreview();
            }

            return;
        }

        if (Mouse.current.leftButton
            .wasPressedThisFrame)
        {
            dragStartVoxel = GetCurrentToolVoxel();
            isDraggingTool = true;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame &&
            isDraggingTool)
        {
            Vector3Int dragEndVoxel = GetCurrentToolVoxel();
            isDraggingTool = false;
            ApplyTool(dragStartVoxel, dragEndVoxel);
            ClearToolPreview();
        }
    }

    private void UpdateToolPreview()
    {
        if (!isDraggingTool ||
            !hasValidVoxelTarget ||
            highlight == null)
        {
            ClearToolPreview();
            return;
        }

        Vector3Int end = GetCurrentToolVoxel();

        if (selectedShape == EditorShape.Box)
        {
            HideLinePreview();
            ShowBoxPreview(dragStartVoxel, end);
            return;
        }

        HideBoxPreview();

        if (!TryBuildShape(dragStartVoxel, end, out List<Vector3Int> positions))
        {
            HideLinePreview();
            return;
        }

        EnsureLinePreviewCapacity(positions.Count);

        for (int index = 0; index < linePreviewMarkers.Count; index++)
        {
            bool visible = index < positions.Count;
            Transform marker = linePreviewMarkers[index];

            marker.gameObject.SetActive(visible);

            if (visible)
            {
                marker.position = (Vector3)positions[index] + Vector3.one * 0.5f;
                marker.localScale = highlightBaseScale;
            }
        }
    }

    private void ShowBoxPreview(Vector3Int start, Vector3Int end)
    {
        if (boxPreviewMarker == null)
        {
            boxPreviewMarker = CreatePreviewMarker("Box Preview");
        }

        Vector3Int minimum = Vector3Int.Min(start, end);
        Vector3Int maximum = Vector3Int.Max(start, end);
        Vector3 size = (Vector3)(maximum - minimum + Vector3Int.one);

        boxPreviewMarker.position =
            (Vector3)minimum + size * 0.5f;

        boxPreviewMarker.localScale =
            Vector3.Scale(highlightBaseScale, size);

        boxPreviewMarker.gameObject.SetActive(true);
    }

    private void EnsureLinePreviewCapacity(int count)
    {
        while (linePreviewMarkers.Count < count)
        {
            linePreviewMarkers.Add(
                CreatePreviewMarker("Line Preview"));
        }
    }

    private void ClearToolPreview()
    {
        HideLinePreview();
        HideBoxPreview();
    }

    private void HideLinePreview()
    {
        foreach (Transform marker in linePreviewMarkers)
        {
            if (marker != null)
            {
                marker.gameObject.SetActive(false);
            }
        }
    }

    private void HideBoxPreview()
    {
        if (boxPreviewMarker != null)
        {
            boxPreviewMarker.gameObject.SetActive(false);
        }
    }

    private Transform CreatePreviewMarker(string markerName)
    {
        Transform marker = Instantiate(highlight, transform);
        marker.name = markerName;

        foreach (Collider markerCollider in marker.GetComponentsInChildren<Collider>())
        {
            markerCollider.enabled = false;
        }

        marker.gameObject.SetActive(false);
        return marker;
    }

    private Vector3Int GetCurrentToolVoxel()
    {
        return selectedAction == EditorAction.Place
            ? placementVoxel
            : hoveredVoxel;
    }

    private void ApplyTool(Vector3Int start, Vector3Int end)
    {
        if (!TryBuildShape(start, end, out List<Vector3Int> positions))
        {
            return;
        }

        VoxelType targetType = selectedAction == EditorAction.Place
            ? selectedVoxelType
            : VoxelType.Air;

        Dictionary<Vector3Int, Voxel> previousVoxels = new();

        foreach (Vector3Int position in positions)
        {
            Voxel previous = voxelWorld.GetVoxel(position);

            if (previous.Type == targetType)
            {
                continue;
            }

            previousVoxels[position] = previous;
        }

        if (previousVoxels.Count == 0)
        {
            return;
        }

        voxelWorld.SetVoxels(previousVoxels.Keys, targetType);
        PushUndo(previousVoxels);

        Debug.Log(
            $"{selectedAction} {selectedShape}: changed "
            + $"{previousVoxels.Count} voxel(s).");
    }

    private bool TryBuildShape(
        Vector3Int start,
        Vector3Int end,
        out List<Vector3Int> positions)
    {
        positions = new List<Vector3Int>();

        if (selectedShape == EditorShape.Single)
        {
            positions.Add(end);
            return true;
        }

        Vector3Int difference = end - start;

        if (selectedShape == EditorShape.Line)
        {
            int steps = Mathf.Max(
                Mathf.Abs(difference.x),
                Mathf.Abs(difference.y),
                Mathf.Abs(difference.z));

            if (steps + 1 > maximumVoxelsPerOperation)
            {
                WarnOperationTooLarge(steps + 1);
                return false;
            }

            if (steps == 0)
            {
                positions.Add(start);
                return true;
            }

            HashSet<Vector3Int> unique = new();

            for (int step = 0; step <= steps; step++)
            {
                Vector3Int position = Vector3Int.RoundToInt(
                    Vector3.Lerp(start, end, step / (float)steps));

                if (unique.Add(position))
                {
                    positions.Add(position);
                }
            }

            return true;
        }

        Vector3Int minimum = Vector3Int.Min(start, end);
        Vector3Int maximum = Vector3Int.Max(start, end);

        long count =
            (long)(maximum.x - minimum.x + 1) *
            (maximum.y - minimum.y + 1) *
            (maximum.z - minimum.z + 1);

        if (count > maximumVoxelsPerOperation)
        {
            WarnOperationTooLarge(count);
            return false;
        }

        positions.Capacity = (int)count;

        for (int x = minimum.x; x <= maximum.x; x++)
        {
            for (int y = minimum.y; y <= maximum.y; y++)
            {
                for (int z = minimum.z; z <= maximum.z; z++)
                {
                    positions.Add(new Vector3Int(x, y, z));
                }
            }
        }

        return true;
    }

    private void WarnOperationTooLarge(long voxelCount)
    {
        Debug.LogWarning(
            $"Editor operation rejected: {voxelCount} voxels exceeds "
            + $"the limit of {maximumVoxelsPerOperation}.",
            this);
    }

    private void PushUndo(Dictionary<Vector3Int, Voxel> previousVoxels)
    {
        undoHistory.Push(previousVoxels);

        if (undoHistory.Count <= maximumUndoSteps)
        {
            return;
        }

        Dictionary<Vector3Int, Voxel>[] history = undoHistory.ToArray();
        undoHistory.Clear();

        int retainedCount = Mathf.Min(maximumUndoSteps, history.Length);

        for (int index = retainedCount - 1; index >= 0; index--)
        {
            undoHistory.Push(history[index]);
        }
    }

    private void UndoLastOperation()
    {
        if (voxelWorld == null || undoHistory.Count == 0)
        {
            return;
        }

        Dictionary<Vector3Int, Voxel> previousVoxels = undoHistory.Pop();
        Dictionary<Voxel, List<Vector3Int>> groupedPositions = new();

        foreach (var pair in previousVoxels)
        {
            if (!groupedPositions.TryGetValue(pair.Value, out List<Vector3Int> group))
            {
                group = new List<Vector3Int>();
                groupedPositions.Add(pair.Value, group);
            }

            group.Add(pair.Key);
        }

        foreach (var pair in groupedPositions)
        {
            voxelWorld.SetVoxels(pair.Value, pair.Key.Type, pair.Key.Facing);
        }

        Debug.Log($"Undid editor operation affecting {previousVoxels.Count} voxel(s).");
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

        if (Keyboard.current.digit9Key
            .wasPressedThisFrame)
        {
            SelectVoxelType(
                VoxelType.ExitPoint,
                "ExitPoint");
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

    private void OnGUI()
    {
        GUI.matrix = Matrix4x4.TRS(
            Vector3.zero,
            Quaternion.identity,
            Vector3.one * EditorUiScale);

        Rect panel = GetEditorPanelRect();

        GUILayout.BeginArea(
            panel,
            "Voxel Tools",
            GUI.skin.window);

        GUILayout.Label($"Material: {selectedVoxelType}");

        GUILayout.BeginHorizontal();
        DrawShapeButton(EditorShape.Single);
        DrawShapeButton(EditorShape.Line);
        DrawShapeButton(EditorShape.Box);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        DrawActionButton(EditorAction.Place);
        DrawActionButton(EditorAction.Erase);
        GUILayout.EndHorizontal();

        GUILayout.Label("LMB drag: apply   Ctrl+Z: undo");

        GUILayout.EndArea();
    }

    private void DrawShapeButton(EditorShape shape)
    {
        bool wasSelected = selectedShape == shape;
        bool selectedNow = GUILayout.Toggle(
            wasSelected,
            shape.ToString(),
            GUI.skin.button);

        if (selectedNow && !wasSelected)
        {
            selectedShape = shape;
            isDraggingTool = false;
            ClearToolPreview();
        }
    }

    private void DrawActionButton(EditorAction action)
    {
        bool wasSelected = selectedAction == action;
        bool selectedNow = GUILayout.Toggle(
            wasSelected,
            action.ToString(),
            GUI.skin.button);

        if (selectedNow && !wasSelected)
        {
            selectedAction = action;
            isDraggingTool = false;
            ClearToolPreview();
        }
    }

    private static Rect GetEditorPanelRect()
    {
        float logicalScreenWidth =
            Screen.width / EditorUiScale;

        return new Rect(
            logicalScreenWidth - EditorPanelWidth - EditorPanelMargin,
            55f,
            EditorPanelWidth,
            EditorPanelHeight);
    }

    private static bool IsPointerOverEditorControls()
    {
        if (Mouse.current == null)
        {
            return false;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();

        Vector2 guiPosition = new(
            screenPosition.x / EditorUiScale,
            (Screen.height - screenPosition.y) / EditorUiScale);

        return GetEditorPanelRect().Contains(guiPosition);
    }

    private static bool IsPointerOverUI()
    {
        return
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject();
    }
}
