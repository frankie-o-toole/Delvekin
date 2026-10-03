using System;

public sealed class LevelAttemptResult
{
    public LevelOutcome Outcome { get; }
    public int TotalDwarves { get; }
    public int RequiredRescues { get; }
    public int Spawned { get; }
    public int Rescued { get; }
    public int Died { get; }
    public int Recalled { get; }
    public int ActiveAtEnd { get; }
    public int UnspawnedAtEnd { get; }
    public double SimulationSeconds { get; }

    public int LeftBehind =>
        ActiveAtEnd + UnspawnedAtEnd;

    public double RescuePercentage =>
        TotalDwarves > 0
            ? Rescued * 100d / TotalDwarves
            : 0d;

    public TimeSpan SimulationTime =>
        TimeSpan.FromSeconds(SimulationSeconds);

    public LevelAttemptResult(
        LevelOutcome outcome,
        int totalDwarves,
        int requiredRescues,
        int spawned,
        int rescued,
        int died,
        int recalled,
        int activeAtEnd,
        int unspawnedAtEnd,
        double simulationSeconds)
    {
        Outcome = outcome;
        TotalDwarves = Math.Max(1, totalDwarves);
        RequiredRescues = Math.Max(1, requiredRescues);
        Spawned = Math.Max(0, spawned);
        Rescued = Math.Max(0, rescued);
        Died = Math.Max(0, died);
        Recalled = Math.Max(0, recalled);
        ActiveAtEnd = Math.Max(0, activeAtEnd);
        UnspawnedAtEnd = Math.Max(0, unspawnedAtEnd);
        SimulationSeconds = Math.Max(0d, simulationSeconds);
    }
}
