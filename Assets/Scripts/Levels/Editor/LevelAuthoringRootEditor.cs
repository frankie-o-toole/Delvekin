using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(LevelAuthoringRoot))]
public sealed class LevelAuthoringRootEditor : Editor
{
    private const float MaximumRayDistance = 2000f;
    private const float WaterSampleDistance = 0.2f;
    private const int MaximumPreviewCubes = 2048;

    private readonly Stack<List<LevelVoxelState>> undoHistory = new();
    private readonly Stack<List<LevelVoxelState>> redoHistory = new();
    private readonly BoxBoundsHandle prefabCaptureBoundsHandle = new();
    private readonly BoxBoundsHandle worldBoundsHandle = new();

    private bool isDragging;
    private Vector3Int dragStart;
    private Vector3Int currentVoxel;
    private bool hasCurrentVoxel;
    private bool editWorldBounds;
    private Vector3Int proposedBoundsOrigin;
    private Vector3Int proposedBoundsSize = Vector3Int.one;
    private LevelDefinition boundsDraftDefinition;
    private string boundsValidationMessage;

    private LevelAuthoringRoot Root =>
        (LevelAuthoringRoot)target;

    public override void OnInspectorGUI()
    {
        LevelDefinition previousDefinition =
            Root.Definition;

        DrawDefaultInspector();

        LevelAuthoringRoot root = Root;

        if (!Application.isPlaying &&
            previousDefinition != root.Definition &&
            root.World != null)
        {
            if (root.Definition != null)
            {
                root.RebuildPreview();
            }
            else
            {
                root.World.SetStartingLevel(null);
                root.ClearPreview();
            }

            EditorUtility.SetDirty(root.World);
            SceneView.RepaintAll();
        }

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

        DrawWorldBoundsInspector(root);

        using (new EditorGUI.DisabledScope(
                   Application.isPlaying ||
                   root.Definition == null ||
                   root.World == null))
        {
            if (GUILayout.Button("Rebuild Edit Mode Preview"))
            {
                if (root.RebuildPreview())
                {
                    EditorUtility.SetDirty(root.Definition);
                    AssetDatabase.SaveAssets();
                }

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

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "Authoring Entities",
            EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(
                   Application.isPlaying ||
                   root.Definition == null ||
                   root.World == null))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Create Water Source"))
            {
                CreateWaterPortal<WaterSourcePortal>(
                    root,
                    "Water Source");
            }

            if (GUILayout.Button("Create Water Outlet"))
            {
                CreateWaterPortal<WaterOutletPortal>(
                    root,
                    "Water Outlet");
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Create Spawn House"))
            {
                CreateSpawnHouse(root);
            }

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Capture Entities"))
            {
                Undo.RecordObject(
                    root.Definition,
                    "Capture Authoring Entities");

                if (root.CaptureAuthoringEntities())
                {
                    EditorUtility.SetDirty(root.Definition);
                    AssetDatabase.SaveAssets();
                }
            }

            if (GUILayout.Button("Rebuild Entities"))
            {
                root.RebuildAuthoringEntities();
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "Voxel Prefab Capture",
            EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(
                   Application.isPlaying ||
                   root.Definition == null ||
                   root.PrefabCaptureTarget == null))
        {
            if (GUILayout.Button("Capture Box To Voxel Prefab"))
            {
                Undo.RecordObject(
                    root.PrefabCaptureTarget,
                    "Capture Voxel Prefab");

                int captured = root.CaptureVoxelPrefab(
                    out int capturedEntities);

                if (captured >= 0)
                {
                    EditorUtility.SetDirty(
                        root.PrefabCaptureTarget);

                    AssetDatabase.SaveAssets();

                    Debug.Log(
                        $"Captured {captured} non-Air voxel(s) and " +
                        $"{capturedEntities} authoring entity/entities in a " +
                        $"{root.PrefabCaptureSize} volume to " +
                        $"'{root.PrefabCaptureTarget.name}'.",
                        root.PrefabCaptureTarget);
                }
                else
                {
                    Debug.LogWarning(
                        "Voxel prefab capture failed. Ensure the entire " +
                        "yellow capture volume is inside the level bounds " +
                        "and does not exceed Maximum Voxels Per Operation.",
                        root);
                }
            }
        }

        EditorGUILayout.HelpBox(
            "The yellow wireframe is the capture volume. Captured voxels " +
            "and fully enclosed authoring entities are normalized to local " +
            "coordinates; implicit Air and the full volume size are preserved.",
            MessageType.None);

        EditorGUILayout.HelpBox(
            "Authoring entities are owned by the LevelDefinition. Edit Mode " +
            "objects are editable proxies; Play Mode receives isolated " +
            "runtime copies.",
            MessageType.None);

        EditorGUILayout.HelpBox(
            Application.isPlaying
                ? "Runtime changes affect the world copy. Capture only " +
                  "when you deliberately want to replace the authored asset."
                : "Select this object and use LMB in the Scene View. Drag " +
                  "for Line/Box. Hold Shift to axis-lock Line. Alt remains " +
                  "available for Scene View navigation.",
            MessageType.None);
    }

    private static void CreateWaterPortal<T>(
        LevelAuthoringRoot root,
        string objectName)
        where T : WaterPortal
    {
        Transform entityRoot = root.FindAuthoringEntitiesRoot();

        if (entityRoot == null)
        {
            GameObject parent =
                new("Authoring Entities");

            Undo.RegisterCreatedObjectUndo(
                parent,
                "Create Authoring Entities Root");

            parent.transform.SetParent(root.transform, false);
            entityRoot = parent.transform;
        }

        Vector3Int minimum = GetSuggestedPortalVoxel(root);

        GameObject portalObject = new(objectName);

        Undo.RegisterCreatedObjectUndo(
            portalObject,
            $"Create {objectName}");

        portalObject.transform.SetParent(entityRoot, true);

        T portal = portalObject.AddComponent<T>();

        portal.ConfigureIdentity(
            null,
            root.Definition);

        portal.Configure(
            root.World,
            minimum,
            Vector3Int.one,
            PuzzleSide.North);

        Undo.RecordObject(
            root.Definition,
            $"Create {objectName} Record");

        root.SynchronizeAuthoringPortal(portal);
        EditorUtility.SetDirty(root.Definition);

        Selection.activeGameObject = portalObject;
        EditorGUIUtility.PingObject(portalObject);
        SceneView.RepaintAll();
    }

    private static void CreateSpawnHouse(
        LevelAuthoringRoot root)
    {
        Transform entityRoot = root.FindAuthoringEntitiesRoot();

        if (entityRoot == null)
        {
            GameObject parent = new("Authoring Entities");

            Undo.RegisterCreatedObjectUndo(
                parent,
                "Create Authoring Entities Root");

            parent.transform.SetParent(root.transform, false);
            entityRoot = parent.transform;
        }

        Vector3Int anchor = GetSuggestedPortalVoxel(root);
        Vector3Int size = new(5, 4, 5);

        GameObject houseObject = new("Spawn House");

        Undo.RegisterCreatedObjectUndo(
            houseObject,
            "Create Spawn House");

        houseObject.transform.SetParent(entityRoot, true);

        SpawnHouseAuthoring house =
            houseObject.AddComponent<SpawnHouseAuthoring>();

        house.ConfigureIdentity(null, root.Definition);
        house.Configure(
            root.World,
            (Vector3)anchor + new Vector3(0.5f, 2f, 0.5f),
            Quaternion.identity,
            size,
            new Vector3(0f, -1.5f, 3f),
            PuzzleSide.North,
            configuredVisualPrefab: null,
            isRuntimeCopy: false);

        Undo.RecordObject(
            root.Definition,
            "Create Spawn House Record");

        root.SynchronizeSpawnHouse(house);
        EditorUtility.SetDirty(root.Definition);

        Selection.activeGameObject = houseObject;
        EditorGUIUtility.PingObject(houseObject);
        SceneView.RepaintAll();
    }

    private static Vector3Int GetSuggestedPortalVoxel(
        LevelAuthoringRoot root)
    {
        Vector3Int candidate;

        if (SceneView.lastActiveSceneView != null)
        {
            candidate = Vector3Int.FloorToInt(
                SceneView.lastActiveSceneView.pivot);
        }
        else
        {
            Vector3 minimum =
                (Vector3)(
                    root.Definition.OriginInChunks *
                    Chunk.ChunkSize);

            Vector3 size =
                (Vector3)(
                    root.Definition.SizeInChunks *
                    Chunk.ChunkSize);

            candidate = Vector3Int.FloorToInt(
                minimum + size * 0.5f);
        }

        if (root.Definition.ContainsWorldPosition(candidate))
        {
            return candidate;
        }

        Vector3Int minimumVoxel =
            root.Definition.OriginInChunks *
            Chunk.ChunkSize;

        Vector3Int maximumVoxel =
            minimumVoxel +
            root.Definition.SizeInChunks *
            Chunk.ChunkSize -
            Vector3Int.one;

        return new Vector3Int(
            Mathf.Clamp(candidate.x, minimumVoxel.x, maximumVoxel.x),
            Mathf.Clamp(candidate.y, minimumVoxel.y, maximumVoxel.y),
            Mathf.Clamp(candidate.z, minimumVoxel.z, maximumVoxel.z));
    }

    private void OnSceneGUI()
    {
        LevelAuthoringRoot root = Root;
        Event current = Event.current;

        if (!Application.isPlaying &&
            editWorldBounds &&
            root.Definition != null)
        {
            DrawWorldBounds(root);
            return;
        }

        if (!Application.isPlaying &&
            root.Definition != null &&
            root.PrefabCaptureTarget != null)
        {
            DrawPrefabCaptureBounds(root);
        }

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
            hasCurrentVoxel &&
            HandleUtility.nearestControl == controlId)
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

    private void DrawWorldBoundsInspector(
        LevelAuthoringRoot root)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "World Bounds",
            EditorStyles.boldLabel);

        if (root.Definition == null)
        {
            return;
        }

        if (boundsDraftDefinition != root.Definition)
        {
            boundsDraftDefinition = root.Definition;
            proposedBoundsOrigin =
                root.Definition.OriginInChunks;
            proposedBoundsSize =
                root.Definition.SizeInChunks;
            boundsValidationMessage = null;
        }

        proposedBoundsOrigin = EditorGUILayout.Vector3IntField(
            "Origin In Chunks",
            proposedBoundsOrigin);

        proposedBoundsSize = EditorGUILayout.Vector3IntField(
            "Size In Chunks",
            proposedBoundsSize);

        proposedBoundsSize = new Vector3Int(
            Mathf.Max(1, proposedBoundsSize.x),
            Mathf.Max(1, proposedBoundsSize.y),
            Mathf.Max(1, proposedBoundsSize.z));

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Apply World Bounds"))
            {
                TryApplyWorldBounds(
                    root,
                    proposedBoundsOrigin,
                    proposedBoundsSize);
            }

            bool requestedEdit = GUILayout.Toggle(
                editWorldBounds,
                "Edit World Bounds In Scene",
                "Button");

            if (requestedEdit != editWorldBounds)
            {
                editWorldBounds = requestedEdit;
                SceneView.RepaintAll();
            }
        }

        EditorGUILayout.HelpBox(
            "Bounds snap to complete 16×16×16 chunks. Expanding adds empty " +
            "authoring space. Shrinking is blocked if it would exclude " +
            "voxels, water portals, or a Spawn House marker.",
            MessageType.None);

        if (!string.IsNullOrWhiteSpace(boundsValidationMessage))
        {
            EditorGUILayout.HelpBox(
                boundsValidationMessage,
                MessageType.Error);
        }
    }

    private void DrawWorldBounds(LevelAuthoringRoot root)
    {
        Vector3 size =
            (Vector3)(root.Definition.SizeInChunks * Chunk.ChunkSize);

        worldBoundsHandle.center =
            (Vector3)(root.Definition.OriginInChunks * Chunk.ChunkSize) +
            size * 0.5f;

        worldBoundsHandle.size = size;
        worldBoundsHandle.handleColor =
            new Color(0.2f, 0.85f, 1f, 0.95f);

        EditorGUI.BeginChangeCheck();
        worldBoundsHandle.DrawHandle();

        Vector3 movedCenter = Handles.PositionHandle(
            worldBoundsHandle.center,
            Quaternion.identity);

        if (!EditorGUI.EndChangeCheck())
        {
            return;
        }

        Vector3 rawMinimum =
            movedCenter - worldBoundsHandle.size * 0.5f;

        Vector3 rawMaximum =
            movedCenter + worldBoundsHandle.size * 0.5f;

        Vector3Int minimumChunk = new(
            Mathf.RoundToInt(rawMinimum.x / Chunk.ChunkSize),
            Mathf.RoundToInt(rawMinimum.y / Chunk.ChunkSize),
            Mathf.RoundToInt(rawMinimum.z / Chunk.ChunkSize));

        Vector3Int maximumChunkExclusive = new(
            Mathf.RoundToInt(rawMaximum.x / Chunk.ChunkSize),
            Mathf.RoundToInt(rawMaximum.y / Chunk.ChunkSize),
            Mathf.RoundToInt(rawMaximum.z / Chunk.ChunkSize));

        Vector3Int chunkSize = maximumChunkExclusive - minimumChunk;
        chunkSize = new Vector3Int(
            Mathf.Max(1, chunkSize.x),
            Mathf.Max(1, chunkSize.y),
            Mathf.Max(1, chunkSize.z));

        TryApplyWorldBounds(root, minimumChunk, chunkSize);
    }

    private void TryApplyWorldBounds(
        LevelAuthoringRoot root,
        Vector3Int originInChunks,
        Vector3Int sizeInChunks)
    {
        if (root.Definition.OriginInChunks == originInChunks &&
            root.Definition.SizeInChunks == sizeInChunks)
        {
            return;
        }

        Undo.RecordObject(
            root.Definition,
            "Edit Level World Bounds");

        if (!root.Definition.TrySetBounds(
                originInChunks,
                sizeInChunks,
                out string failureReason))
        {
            boundsValidationMessage = failureReason;
            Repaint();
            SceneView.RepaintAll();
            return;
        }

        proposedBoundsOrigin = originInChunks;
        proposedBoundsSize = sizeInChunks;
        boundsValidationMessage = null;

        EditorUtility.SetDirty(root.Definition);
        root.RebuildPreview();
        SceneView.RepaintAll();
        Repaint();
    }

    private void DrawPrefabCaptureBounds(
        LevelAuthoringRoot root)
    {
        Vector3 currentSize = root.PrefabCaptureSize;

        prefabCaptureBoundsHandle.center =
            (Vector3)root.PrefabCaptureMinimum +
            currentSize * 0.5f;

        prefabCaptureBoundsHandle.size = currentSize;
        prefabCaptureBoundsHandle.handleColor =
            new Color(1f, 0.75f, 0.1f, 0.95f);

        EditorGUI.BeginChangeCheck();

        prefabCaptureBoundsHandle.DrawHandle();

        Vector3 movedCenter = Handles.PositionHandle(
            prefabCaptureBoundsHandle.center,
            Quaternion.identity);

        if (!EditorGUI.EndChangeCheck())
        {
            return;
        }

        Vector3 rawSize = prefabCaptureBoundsHandle.size;

        Vector3Int snappedSize = new(
            Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(rawSize.x))),
            Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(rawSize.y))),
            Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(rawSize.z))));

        Vector3Int snappedMinimum = Vector3Int.RoundToInt(
            movedCenter - (Vector3)snappedSize * 0.5f);

        Undo.RecordObject(
            root,
            "Edit voxel prefab capture bounds");

        root.SetPrefabCaptureBounds(
            snappedMinimum,
            snappedSize);

        EditorUtility.SetDirty(root);
        SceneView.RepaintAll();
        Repaint();
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
                out Vector3Int fluidPosition))
        {
            currentVoxel = fluidPosition;
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

    private static bool TryFindFluidAlongRay(
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
