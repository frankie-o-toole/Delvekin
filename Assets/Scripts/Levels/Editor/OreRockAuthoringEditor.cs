using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(OreRockAuthoring))]
public sealed class OreRockAuthoringEditor : Editor
{
    public override void OnInspectorGUI()
    {
        OreRockAuthoring oreRock =
            (OreRockAuthoring)target;

        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();

        EditorGUILayout.HelpBox(
            "Each dwarf will extract one unit. The total level capacity is " +
            "derived automatically from every Ore Rock in the level.",
            MessageType.Info);

        if (!EditorGUI.EndChangeCheck())
        {
            return;
        }

        oreRock.RefreshVisual();
        Synchronize(oreRock);
        SceneView.RepaintAll();
    }

    private static void Synchronize(OreRockAuthoring oreRock)
    {
        LevelAuthoringRoot root =
            oreRock.GetComponentInParent<LevelAuthoringRoot>();

        if (root == null || root.Definition == null)
        {
            EditorUtility.SetDirty(oreRock);
            return;
        }

        Undo.RecordObject(
            root.Definition,
            "Update Ore Rock");

        if (root.SynchronizeOreRock(oreRock))
        {
            EditorUtility.SetDirty(root.Definition);
        }

        EditorUtility.SetDirty(oreRock);
    }
}
