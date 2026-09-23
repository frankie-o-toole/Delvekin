using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(WaterPortal), true)]
[CanEditMultipleObjects]
public sealed class WaterPortalEditor : Editor
{
    private readonly BoxBoundsHandle boundsHandle = new();

    private WaterPortal Portal => (WaterPortal)target;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool changed = EditorGUI.EndChangeCheck();

        serializedObject.ApplyModifiedProperties();

        if (changed)
        {
            foreach (Object selected in targets)
            {
                WaterPortal portal = (WaterPortal)selected;
                portal.SnapToVoxelGrid();
                EditorUtility.SetDirty(portal);
                Synchronize(portal);
            }

            SceneView.RepaintAll();
        }

        if (targets.Length != 1)
        {
            return;
        }

        EditorGUILayout.Space();
        DrawValidation(Portal);
    }

    private void OnSceneGUI()
    {
        WaterPortal portal = Portal;

        if (portal.transform.hasChanged)
        {
            portal.SnapToVoxelGrid();
            portal.transform.hasChanged = false;
            Synchronize(portal);
        }

        boundsHandle.center =
            (Vector3)portal.MinimumVoxel +
            (Vector3)portal.Size * 0.5f;

        boundsHandle.size = portal.Size;

        boundsHandle.SetColor(
            portal is WaterSourcePortal
                ? new Color(0.15f, 1f, 0.35f, 0.9f)
                : new Color(1f, 0.2f, 0.85f, 0.9f));

        EditorGUI.BeginChangeCheck();
        boundsHandle.DrawHandle();

        if (!EditorGUI.EndChangeCheck())
        {
            DrawFacingLabel(portal);
            return;
        }

        Undo.RecordObjects(
            new Object[] { portal, portal.transform },
            "Resize Water Portal");

        Vector3Int size = new(
            Mathf.Max(1, Mathf.RoundToInt(boundsHandle.size.x)),
            Mathf.Max(1, Mathf.RoundToInt(boundsHandle.size.y)),
            Mathf.Max(1, Mathf.RoundToInt(boundsHandle.size.z)));

        Vector3 minimumFloat =
            boundsHandle.center - (Vector3)size * 0.5f;

        Vector3Int minimum =
            Vector3Int.RoundToInt(minimumFloat);

        portal.Configure(
            portal.World,
            minimum,
            size,
            portal.Facing);

        EditorUtility.SetDirty(portal);
        Synchronize(portal);
        DrawFacingLabel(portal);
    }

    private static void Synchronize(WaterPortal portal)
    {
        LevelAuthoringRoot root =
            portal.GetComponentInParent<LevelAuthoringRoot>();

        if (root == null ||
            root.Definition == null)
        {
            return;
        }

        Undo.RecordObject(
            root.Definition,
            "Edit Authoring Entity");

        if (root.SynchronizeAuthoringPortal(portal))
        {
            EditorUtility.SetDirty(root.Definition);
        }
    }

    private static void DrawFacingLabel(WaterPortal portal)
    {
        Handles.color = Color.white;

        Handles.Label(
            (Vector3)portal.MinimumVoxel +
            (Vector3)portal.Size * 0.5f +
            Vector3.up * (portal.Size.y * 0.5f + 0.35f),
            $"{portal.GetType().Name}\nFacing: {portal.Facing}");
    }

    private static void DrawValidation(WaterPortal portal)
    {
        LevelAuthoringRoot root =
            portal.GetComponentInParent<LevelAuthoringRoot>();

        if (root == null || root.Definition == null)
        {
            EditorGUILayout.HelpBox(
                "Portal must be a child of a configured " +
                "LevelAuthoringRoot.",
                MessageType.Error);
            return;
        }

        List<Vector3Int> positions = new();
        portal.GetCoveredVoxels(positions);

        int outside = 0;
        int water = 0;

        foreach (Vector3Int position in positions)
        {
            if (!root.Definition.ContainsWorldPosition(position))
            {
                outside++;
                continue;
            }

            bool isWater =
                root.Definition.TryGetVoxelRecord(
                    position,
                    out LevelVoxelRecord record) &&
                record.Type == VoxelType.Water;

            if (isWater)
            {
                water++;
            }
        }

        if (outside > 0)
        {
            EditorGUILayout.HelpBox(
                $"{outside} portal cell(s) are outside the level bounds.",
                MessageType.Error);
            return;
        }

        if (water != positions.Count)
        {
            EditorGUILayout.HelpBox(
                $"Portal overlaps {water}/{positions.Count} Water cells. " +
                "Every portal cell should overlap authored Water.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox(
            $"Valid portal: {water} Water cell(s), facing " +
            $"{portal.Facing}.",
            MessageType.Info);
    }
}
