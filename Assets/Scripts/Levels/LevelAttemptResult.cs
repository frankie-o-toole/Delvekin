using System;

public sealed class LevelAttemptResult
{
    public LevelOutcome Outcome { get; }
    public int TotalDwarves { get; }
    public int TotalOreCapacity { get; }
    public int RequiredMinedResources { get; }
    public int Spawned { get; }
    public int MinedResources { get; }
    public int Died { get; }
    public int Recalled { get; }
    public int ActiveAtEnd { get; }
    public int UnspawnedAtEnd { get; }
    public double SimulationSeconds { get; }

    public int LeftBehind =>
        ActiveAtEnd + UnspawnedAtEnd;

    public double MinedPercentage =>
        TotalOreCapacity > 0
            ? MinedResources * 100d / TotalOreCapacity
            : 0d;

    public TimeSpan SimulationTime =>
        TimeSpan.FromSeconds(SimulationSeconds);

    public LevelAttemptResult(
        LevelOutcome outcome,
        int totalDwarves,
        int totalOreCapacity,
        int requiredMinedResources,
        int spawned,
        int minedResources,
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
        MinedResources = Math.Max(0, minedResources);
        Died = Math.Max(0, died);
        Recalled = Math.Max(0, recalled);
        ActiveAtEnd = Math.Max(0, activeAtEnd);
        UnspawnedAtEnd = Math.Max(0, unspawnedAtEnd);
        SimulationSeconds = Math.Max(0d, simulationSeconds);
    }
}
