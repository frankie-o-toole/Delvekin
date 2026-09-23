using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class LevelPrefabPlacementSceneTool
{
    private const float MaximumRayDistance = 2000f;
    private const int MaximumPreviewCubes = 2048;

    private static Vector3Int currentOrigin;
    private static bool hasCurrentOrigin;

    static LevelPrefabPlacementSceneTool()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed += RebuildSelectedPreview;
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        LevelAuthoringRoot root = GetSelectedRoot();

        if (Application.isPlaying ||
            root == null ||
            !root.PrefabPlacementEnabled ||
            root.PrefabPlacementSource == null ||
            root.Definition == null ||
            root.World == null)
        {
            hasCurrentOrigin = false;
            return;
        }

        Event current = Event.current;

        if (current.type == EventType.KeyDown &&
            current.keyCode == KeyCode.R &&
            !current.alt &&
            !current.control &&
            !current.command)
        {
            Undo.RecordObject(root, "Rotate voxel prefab");
            root.RotatePrefabPlacement();
            EditorUtility.SetDirty(root);
            current.Use();
            SceneView.RepaintAll();
            return;
        }

        UpdateCurrentOrigin(root, current.mousePosition);
        DrawGhost(root);

        int controlId = GUIUtility.GetControlID(
            "DelvekinLevelPrefabPlacement".GetHashCode(),
            FocusType.Passive);

        if (current.type == EventType.Layout && hasCurrentOrigin)
        {
            HandleUtility.AddDefaultControl(controlId);
        }

        if (current.alt ||
            current.type != EventType.MouseDown ||
            current.button != 0 ||
            !hasCurrentOrigin)
        {
            return;
        }

        Undo.RecordObject(
            root.Definition,
            "Place voxel prefab");

        int changed = root.ApplyVoxelPrefab(currentOrigin);

        if (changed >= 0)
        {
            EditorUtility.SetDirty(root.Definition);

            Debug.Log(
                $"Placed '{root.PrefabPlacementSource.DisplayName}' at " +
                $"{currentOrigin} with {root.PrefabPlacementRotation}; " +
                $"changed {changed} voxel(s).",
                root.Definition);
        }
        else
        {
            Debug.LogWarning(
                "Voxel prefab placement failed. Keep the complete ghost " +
                "inside the cyan level bounds and below Maximum Voxels " +
                "Per Operation.",
                root);
        }

        current.Use();
        SceneView.RepaintAll();
    }

    private static LevelAuthoringRoot GetSelectedRoot()
    {
        GameObject selected = Selection.activeGameObject;

        return selected != null
            ? selected.GetComponentInParent<LevelAuthoringRoot>()
            : null;
    }

    private static void UpdateCurrentOrigin(
        LevelAuthoringRoot root,
        Vector2 guiPosition)
    {
        hasCurrentOrigin = false;
        Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                MaximumRayDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore) ||
            !hit.transform.IsChildOf(root.World.transform))
        {
            return;
        }

        currentOrigin = Vector3Int.FloorToInt(
            hit.point + hit.normal * 0.01f);

        hasCurrentOrigin = true;
    }

    private static void DrawGhost(LevelAuthoringRoot root)
    {
        if (!hasCurrentOrigin)
        {
            return;
        }

        bool valid = root.TryBuildPrefabPlacement(
            currentOrigin,
            out List<LevelVoxelState> states,
            out Vector3Int rotatedSize);

        Handles.color = valid
            ? new Color(0.15f, 0.95f, 1f, 0.9f)
            : new Color(1f, 0.2f, 0.15f, 0.95f);

        Handles.DrawWireCube(
            (Vector3)currentOrigin +
            (Vector3)rotatedSize * 0.5f,
            rotatedSize);

        if (!valid)
        {
            return;
        }

        int drawn = 0;

        foreach (LevelVoxelState state in states)
        {
            if (!state.HasVoxel)
            {
                continue;
            }

            Handles.DrawWireCube(
                (Vector3)state.Position + Vector3.one * 0.5f,
                Vector3.one * 1.02f);

            drawn++;

            if (drawn >= MaximumPreviewCubes)
            {
                break;
            }
        }

        Handles.Label(
            (Vector3)currentOrigin + Vector3.up * 0.25f,
            $"LMB place | R rotate | {root.PrefabPlacementMode}");
    }

    private static void RebuildSelectedPreview()
    {
        if (Application.isPlaying)
        {
            return;
        }

        LevelAuthoringRoot root = GetSelectedRoot();

        if (root != null &&
            root.Definition != null &&
            root.World != null)
        {
            root.RebuildPreview();
            SceneView.RepaintAll();
        }
    }
}
