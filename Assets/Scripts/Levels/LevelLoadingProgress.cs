using UnityEngine;

public readonly struct LevelLoadingProgress
{
    public LevelLoadingState State { get; }
    public string Phase { get; }
    public float Progress { get; }
    public int CompletedWork { get; }
    public int TotalWork { get; }

    public bool IsLoading =>
        State != LevelLoadingState.Idle &&
        State != LevelLoadingState.Ready &&
        State != LevelLoadingState.Failed;

    public bool IsReady =>
        State == LevelLoadingState.Ready;

    public bool HasDeterminateProgress =>
        TotalWork > 0;

    public LevelLoadingProgress(
        LevelLoadingState state,
        string phase,
        float progress,
        int completedWork = 0,
        int totalWork = 0)
    {
        State = state;
        Phase = phase ?? string.Empty;
        Progress = Mathf.Clamp01(progress);
        CompletedWork = Mathf.Max(0, completedWork);
        TotalWork = Mathf.Max(0, totalWork);
    }
}
