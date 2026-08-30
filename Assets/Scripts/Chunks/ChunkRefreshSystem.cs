using UnityEngine;

public static class ChunkRefreshSystem
{
    public static System.Action OnRefreshRequested;

    public static System.Action<SliceAxis, int, int>
        OnSliceRefreshRequested;

    public static void RequestFullRefresh()
    {
        OnRefreshRequested?.Invoke();
    }

    public static void RequestSliceRefresh(
        SliceAxis axis,
        int oldVisibleBoundary,
        int newVisibleBoundary)
    {
        OnSliceRefreshRequested?.Invoke(
            axis,
            oldVisibleBoundary,
            newVisibleBoundary);
    }
}
