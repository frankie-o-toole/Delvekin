using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpawnHouseAuthoring))]
public sealed class SpawnHouseAuthoringEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SpawnHouseAuthoring house =
            (SpawnHouseAuthoring)target;

        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();

        if (!house.IsSpawnMarkerInsideWorld)
        {
            EditorGUILayout.HelpBox(
                "The Dwarf Spawn marker is outside the level world bounds. " +
                "Move the marker inside the cyan bounds or expand the " +
                "world bounds before entering Play Mode.",
                MessageType.Error);
        }

        if (!EditorGUI.EndChangeCheck())
        {
            return;
        }

        house.RefreshVisual();
        Synchronize(house);
        SceneView.RepaintAll();
    }

    private void OnSceneGUI()
    {
        SpawnHouseAuthoring house =
            (SpawnHouseAuthoring)target;

        if (Application.isPlaying)
        {
            return;
        }

        Vector3 markerPosition =
            house.transform.TransformPoint(
                house.SpawnMarkerLocalPosition);

        Handles.color = new Color(0.2f, 1f, 0.25f, 1f);
        Handles.Label(
            markerPosition + Vector3.up * 0.75f,
            "Dwarf Spawn");

        EditorGUI.BeginChangeCheck();

        Vector3 movedPosition = Handles.PositionHandle(
            markerPosition,
            Quaternion.identity);

        if (!EditorGUI.EndChangeCheck())
        {
            return;
        }

        Undo.RecordObject(
            house,
            "Move Spawn House Marker");

        house.SetSpawnMarkerWorldPosition(movedPosition);
        Synchronize(house);
        SceneView.RepaintAll();
    }

    private static void Synchronize(
        SpawnHouseAuthoring house)
    {
        LevelAuthoringRoot root =
            house.GetComponentInParent<LevelAuthoringRoot>();

        if (root == null || root.Definition == null)
        {
            EditorUtility.SetDirty(house);
            return;
        }

        Undo.RecordObject(
            root.Definition,
            "Update Spawn House");

        if (root.SynchronizeSpawnHouse(house))
        {
            EditorUtility.SetDirty(root.Definition);
        }

        EditorUtility.SetDirty(house);
    }
}
