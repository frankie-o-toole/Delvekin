using System.IO;
using UnityEngine;

public static class LevelSerializer
{
    public static void Save(SavedLevel level, string fileName)
    {
        string json = JsonUtility.ToJson(level, true);

        string path =
            Path.Combine(
                Application.persistentDataPath,
                fileName + ".json");

        File.WriteAllText(path, json);

        Debug.Log($"Saved level to: {path}");
    }

    public static SavedLevel Load(string fileName)
    {
        string path =
            Path.Combine(
                Application.persistentDataPath,
                fileName + ".json");

        if (!File.Exists(path))
        {
            Debug.LogWarning($"File not found: {path}");
            return null;
        }

        string json = File.ReadAllText(path);

        return JsonUtility.FromJson<SavedLevel>(json);
    }
}