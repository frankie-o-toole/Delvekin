using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HubCampaignDefinition))]
public sealed class HubCampaignDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        HubCampaignDefinition definition =
            (HubCampaignDefinition)target;
        IReadOnlyList<string> errors =
            definition.GetValidationErrors();

        if (errors.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Campaign level graph is valid.",
                MessageType.Info);
            return;
        }

        foreach (string error in errors)
        {
            EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }
}
