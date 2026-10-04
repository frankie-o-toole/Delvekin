using System;

public sealed class LevelAttemptResult
{
    public LevelOutcome Outcome { get; }
    public int TotalDwarves { get; }
    public int TotalOreCapacity { get; }
    public int RequiredMinedResources { get; }
    public int Spawned { get; }
    public int DeliveredResources { get; }
    public int Died { get; }
    public int Recalled { get; }
    public int ActiveAtEnd { get; }
    public int UnspawnedAtEnd { get; }
    public double SimulationSeconds { get; }

    public int LeftBehind =>
        ActiveAtEnd + UnspawnedAtEnd;

    public int RemainingResources =>
        Math.Max(0, TotalOreCapacity - DeliveredResources);

    public bool IsPerfectResourceRun =>
        TotalOreCapacity > 0 &&
        DeliveredResources == TotalOreCapacity;

    public double DeliveredResourcePercentage =>
        TotalOreCapacity > 0
            ? DeliveredResources * 100d / TotalOreCapacity
            : 0d;

    public TimeSpan SimulationTime =>
        TimeSpan.FromSeconds(SimulationSeconds);

    public LevelAttemptResult(
        LevelOutcome outcome,
        int totalDwarves,
        int totalOreCapacity,
        int requiredMinedResources,
        int spawned,
        int deliveredResources,
        int died,
        int recalled,
        int activeAtEnd,
        int unspawnedAtEnd,
        double simulationSeconds)
    {
        Outcome = outcome;
        TotalDwarves = Math.Max(1, totalDwarves);
        TotalOreCapacity = Math.Max(0, totalOreCapacity);
        RequiredMinedResources =
            Math.Max(1, requiredMinedResources);
        Spawned = Math.Max(0, spawned);
        DeliveredResources = Math.Min(
            TotalOreCapacity,
            Math.Max(0, deliveredResources));
        Died = Math.Max(0, died);
        Recalled = Math.Max(0, recalled);
        ActiveAtEnd = Math.Max(0, activeAtEnd);
        UnspawnedAtEnd = Math.Max(0, unspawnedAtEnd);
        SimulationSeconds = Math.Max(0d, simulationSeconds);
    }
}
