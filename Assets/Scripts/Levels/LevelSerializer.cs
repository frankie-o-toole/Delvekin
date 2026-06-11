using System.IO;
using UnityEngine;

public static class LevelSerializer
{
    public static void Save(LevelData data, string name)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(Application.dataPath + $"/{name}.json", json);
    }

    public static LevelData Load(string name)
    {
        string path = Application.dataPath + $"/{name}.json";

        if (!File.Exists(path))
            return null;

        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<LevelData>(json);
    }
}