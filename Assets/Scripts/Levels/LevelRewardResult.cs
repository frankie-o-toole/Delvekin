using System;

public sealed class LevelRewardResult
{
    public string LevelId { get; }
    public bool AttemptSucceeded { get; }
    public bool IsFirstCompletion { get; }
    public int AttemptDelivered { get; }
    public int PreviousBestDelivered { get; }
    public int BestDelivered { get; }
    public int NewlyEarnedResources { get; }
    public int TotalOreCapacity { get; }

    public bool ImprovedBest =>
        BestDelivered > PreviousBestDelivered;

    public LevelRewardResult(
        string levelId,
        bool attemptSucceeded,
        bool isFirstCompletion,
        int attemptDelivered,
        int previousBestDelivered,
        int bestDelivered,
        int newlyEarnedResources,
        int totalOreCapacity)
    {
        LevelId = levelId ?? string.Empty;
        AttemptSucceeded = attemptSucceeded;
        IsFirstCompletion = isFirstCompletion;
        TotalOreCapacity = Math.Max(0, totalOreCapacity);
        AttemptDelivered = ClampToCapacity(
            attemptDelivered,
            TotalOreCapacity);
        PreviousBestDelivered = ClampToCapacity(
            previousBestDelivered,
            TotalOreCapacity);
        BestDelivered = ClampToCapacity(
            bestDelivered,
            TotalOreCapacity);
        NewlyEarnedResources = Math.Max(0, newlyEarnedResources);
    }

    private static int ClampToCapacity(int value, int capacity)
    {
        return Math.Min(capacity, Math.Max(0, value));
    }
}
