using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelAuthoringRoot))]
public sealed class LevelAuthoringRootEditor : Editor
{
    private const float MaximumRayDistance = 2000f;
    private const float WaterSampleDistance = 0.2f;
    private const int MaximumPreviewCubes = 2048;

    private readonly Stack<List<LevelVoxelState>> undoHistory = new();
    private readonly Stack<List<LevelVoxelState>> redoHistory = new();

    private bool isDragging;
    private Vector3Int dragStart;
    private Vector3Int currentVoxel;
    private bool hasCurrentVoxel;

    private LevelAuthoringRoot Root =>
        (LevelAuthoringRoot)target;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LevelAuthoringRoot root = Root;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "Level Authoring",
            EditorStyles.boldLabel);

        if (root.Definition == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a LevelDefinition asset before building or " +
                "capturing a level.",
                MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(
                   Application.isPlaying ||
                   root.Definition == null ||
                   root.World == null))
        {
            if (GUILayout.Button("Rebuild Edit Mode Preview"))
            {
                root.RebuildPreview();
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Clear Edit Mode Preview"))
            {
                root.ClearPreview();
                SceneView.RepaintAll();
            }
        }

        using (new EditorGUI.DisabledScope(
                   !Application.isPlaying ||
                   root.Definition == null ||
                   root.World == null))
        {
            if (GUILayout.Button("Capture Runtime World To Definition"))
            {
                if (root.CaptureRuntimeWorld())
                {
                    EditorUtility.SetDirty(root.Definition);
                    AssetDatabase.SaveAssets();

                    Debug.Log(
                        $"Captured runtime world into " +
                        $"'{root.Definition.name}'.",
                        root.Definition);
                }
            }
        }

        using (new EditorGUI.DisabledScope(
                   Application.isPlaying ||
                   root.Definition == null ||
                   root.World == null))
        {
            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(undoHistory.Count == 0))
            {
                if (GUILayout.Button("Undo Voxel Edit"))
                {
                    UndoVoxelEdit();
                }
            }

            using (new EditorGUI.DisabledScope(redoHistory.Count == 0))
            {
                if (GUILayout.Button("Redo Voxel Edit"))
                {
                    RedoVoxelEdit();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.HelpBox(
            Application.isPlaying
                ? "Runtime changes affect the world copy. Capture only " +
                  "when you deliberately want to replace the authored asset."
                : "Select this object and use LMB in the Scene View. Drag " +
                  "for Line/Box. Hold Shift to axis-lock Line. Alt remains " +
                  "available for Scene View navigation.",
            MessageType.None);
    }

    private void OnSceneGUI()
    {
        LevelAuthoringRoot root = Root;
        Event current = Event.current;

        if (Application.isPlaying ||
            !root.VoxelToolEnabled ||
            root.Definition == null ||
            root.World == null)
        {
            return;
        }

        if (current.type == EventType.KeyDown &&
            (current.control || current.command))
        {
            if (current.keyCode == KeyCode.Z)
            {
                if (current.shift)
                {
                    RedoVoxelEdit();
                }
                else
                {
                    UndoVoxelEdit();
                }

                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.Y)
            {
                RedoVoxelEdit();
                current.Use();
                return;
            }
        }

        UpdateCurrentVoxel(root, current.mousePosition, false);

        int controlId = GUIUtility.GetControlID(
            "DelvekinLevelVoxelTool".GetHashCode(),
            FocusType.Passive);

        if (current.type == EventType.Layout && hasCurrentVoxel)
        {
            HandleUtility.AddDefaultControl(controlId);
        }

        DrawToolPreview(root);

        if (current.alt)
        {
            return;
        }

        if (current.type == EventType.MouseDown &&
            current.button == 0 &&
            hasCurrentVoxel)
        {
            GUIUtility.hotControl = controlId;
            dragStart = currentVoxel;
            isDragging = true;
            current.Use();
            return;
        }

        if (current.type == EventType.MouseDrag &&
            GUIUtility.hotControl == controlId &&
            isDragging)
        {
            current.Use();
            SceneView.RepaintAll();
            return;
        }

        if (current.type == EventType.MouseUp &&
            current.button == 0 &&
            GUIUtility.hotControl == controlId &&
            isDragging)
        {
            GUIUtility.hotControl = 0;
            isDragging = false;

            if (hasCurrentVoxel &&
                TryBuildShape(
                    root,
                    dragStart,
                    currentVoxel,
                    current.shift,
                    out List<Vector3Int> positions))
            {
                List<LevelVoxelState> before =
                    root.CaptureVoxelStates(positions);

                int changed = root.ApplyVoxelEdit(positions);

                if (changed > 0)
                {
                    undoHistory.Push(before);
                    redoHistory.Clear();
                    EditorUtility.SetDirty(root.Definition);

                    Debug.Log(
                        $"{root.Action} {root.Shape}: changed " +
                        $"{changed} authored voxel(s).",
                        root.Definition);

                    SceneView.RepaintAll();
                }
            }

            current.Use();
        }
    }

    private void UpdateCurrentVoxel(
        LevelAuthoringRoot root,
        Vector2 guiPosition,
        bool unused = false)
    {
        hasCurrentVoxel = false;

        Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                MaximumRayDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            return;
        }

        if (!hit.transform.IsChildOf(root.World.transform))
        {
            return;
        }

        if (root.Action == LevelAuthoringAction.Erase &&
            TryFindFluidAlongRay(
                root,
                ray,
                hit.distance + 1f,
                out Vector3Int waterPosition))
        {
            currentVoxel = waterPosition;
            hasCurrentVoxel = true;
            return;
        }

        Vector3 samplePoint =
            root.Action == LevelAuthoringAction.Place
                ? hit.point + hit.normal * 0.01f
                : hit.point - hit.normal * 0.01f;

        currentVoxel = Vector3Int.FloorToInt(samplePoint);
        hasCurrentVoxel =
            root.Definition.ContainsWorldPosition(currentVoxel);
    }

    private static bool TryFindWaterAlongRay(
        LevelAuthoringRoot root,
        Ray ray,
        float maximumDistance,
        out Vector3Int position)
    {
        position = default;
        Vector3Int previous =
            new(int.MinValue, int.MinValue, int.MinValue);

        for (float distance = 0f;
             distance <= maximumDistance;
             distance += WaterSampleDistance)
        {
            Vector3Int candidate =
                Vector3Int.FloorToInt(ray.GetPoint(distance));

            if (candidate == previous)
            {
                continue;
            }

            previous = candidate;

            if (!root.Definition.ContainsWorldPosition(candidate))
            {
                continue;
            }

            VoxelType type = root.World.GetVoxel(candidate).Type;

            if (type == VoxelType.Water ||
                type == VoxelType.Lava)
            {
                position = candidate;
                return true;
            }
        }

        return false;
    }

    private void DrawToolPreview(LevelAuthoringRoot root)
    {
        if (!hasCurrentVoxel)
        {
            return;
        }

        Vector3Int start = isDragging
            ? dragStart
            : currentVoxel;

        Handles.color =
            root.Action == LevelAuthoringAction.Place
                ? new Color(0.2f, 1f, 0.45f, 0.9f)
                : new Color(1f, 0.25f, 0.2f, 0.9f);

        if (root.Shape == LevelAuthoringShape.Box && isDragging)
        {
            Vector3Int minimum = Vector3Int.Min(start, currentVoxel);
            Vector3Int maximum = Vector3Int.Max(start, currentVoxel);
            Vector3 size = (Vector3)(maximum - minimum + Vector3Int.one);
            Vector3 center = (Vector3)minimum + size * 0.5f;

            Handles.DrawWireCube(center, size);
            return;
        }

        if (!TryBuildShape(
                root,
                start,
                currentVoxel,
                Event.current.shift,
                out List<Vector3Int> positions))
        {
            return;
        }

        int drawCount = Mathf.Min(
            positions.Count,
            MaximumPreviewCubes);

        for (int i = 0; i < drawCount; i++)
        {
            Handles.DrawWireCube(
                (Vector3)positions[i] + Vector3.one * 0.5f,
                Vector3.one * 1.02f);
        }
    }

    private static bool TryBuildShape(
        LevelAuthoringRoot root,
        Vector3Int start,
        Vector3Int end,
        bool axisLock,
        out List<Vector3Int> positions)
    {
        positions = new List<Vector3Int>();

        if (root.Shape == LevelAuthoringShape.Single)
        {
            if (root.Definition.ContainsWorldPosition(end))
            {
                positions.Add(end);
            }

            return positions.Count > 0;
        }

        Vector3Int difference = end - start;

        if (root.Shape == LevelAuthoringShape.Line)
        {
            if (axisLock)
            {
                end = LockToDominantAxis(start, end);
                difference = end - start;
            }

            int steps = Mathf.Max(
                Mathf.Abs(difference.x),
                Mathf.Abs(difference.y),
                Mathf.Abs(difference.z));

            if (steps + 1 > root.MaximumVoxelsPerOperation)
            {
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

                if (root.Definition.ContainsWorldPosition(position) &&
                    unique.Add(position))
                {
                    positions.Add(position);
                }
            }

            return positions.Count > 0;
        }

        Vector3Int minimum = Vector3Int.Min(start, end);
        Vector3Int maximum = Vector3Int.Max(start, end);

        long count =
            (long)(maximum.x - minimum.x + 1) *
            (maximum.y - minimum.y + 1) *
            (maximum.z - minimum.z + 1);

        if (count > root.MaximumVoxelsPerOperation)
        {
            return false;
        }

        positions.Capacity = (int)count;

        for (int x = minimum.x; x <= maximum.x; x++)
        {
            for (int y = minimum.y; y <= maximum.y; y++)
            {
                for (int z = minimum.z; z <= maximum.z; z++)
                {
                    Vector3Int position = new(x, y, z);

                    if (root.Definition.ContainsWorldPosition(position))
                    {
                        positions.Add(position);
                    }
                }
            }
        }

        return positions.Count > 0;
    }

    private static Vector3Int LockToDominantAxis(
        Vector3Int start,
        Vector3Int end)
    {
        Vector3Int difference = end - start;
        int x = Mathf.Abs(difference.x);
        int y = Mathf.Abs(difference.y);
        int z = Mathf.Abs(difference.z);

        if (x >= y && x >= z)
        {
            return new Vector3Int(end.x, start.y, start.z);
        }

        return y >= z
            ? new Vector3Int(start.x, end.y, start.z)
            : new Vector3Int(start.x, start.y, end.z);
    }

    private void UndoVoxelEdit()
    {
        ApplyHistory(undoHistory, redoHistory, "Undo voxel edit");
    }

    private void RedoVoxelEdit()
    {
        ApplyHistory(redoHistory, undoHistory, "Redo voxel edit");
    }

    private void ApplyHistory(
        Stack<List<LevelVoxelState>> source,
        Stack<List<LevelVoxelState>> destination,
        string label)
    {
        if (Application.isPlaying ||
            source.Count == 0 ||
            Root == null ||
            Root.Definition == null ||
            Root.World == null)
        {
            return;
        }

        List<LevelVoxelState> states = source.Pop();
        List<Vector3Int> positions = new(states.Count);

        foreach (LevelVoxelState state in states)
        {
            positions.Add(state.Position);
        }

        List<LevelVoxelState> inverse =
            Root.CaptureVoxelStates(positions);

        Root.RestoreVoxelStates(states);
        destination.Push(inverse);

        EditorUtility.SetDirty(Root.Definition);
        Debug.Log(
            $"{label}: restored {states.Count} authored voxel(s).",
            Root.Definition);

        SceneView.RepaintAll();
        Repaint();
    }
}
