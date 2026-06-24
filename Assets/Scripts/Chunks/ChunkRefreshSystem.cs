using UnityEngine;

public static class ChunkRefreshSystem
{
    public static System.Action OnRefreshRequested;

    public static void RequestFullRefresh()
    {
        OnRefreshRequested?.Invoke();
    }
}