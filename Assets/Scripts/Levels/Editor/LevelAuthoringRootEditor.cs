using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelAuthoringRoot))]
public sealed class LevelAuthoringRootEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LevelAuthoringRoot root = (LevelAuthoringRoot)target;

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
                Undo.RecordObject(
                    root.Definition,
                    "Capture Runtime World");

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

        EditorGUILayout.HelpBox(
            Application.isPlaying
                ? "Runtime changes affect the world copy. Capture only " +
                  "when you deliberately want to replace the authored asset."
                : "The preview is generated from the asset and is not " +
                  "saved as scene content.",
            MessageType.None);
    }
}
