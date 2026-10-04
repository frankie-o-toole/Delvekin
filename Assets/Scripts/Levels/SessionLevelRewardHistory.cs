using System.Collections.Generic;

public static class SessionLevelRewardHistory
{
    private readonly struct Entry
    {
        public bool HasCompleted { get; }
        public int BestDelivered { get; }

        public Entry(bool hasCompleted, int bestDelivered)
        {
            HasCompleted = hasCompleted;
            BestDelivered = bestDelivered;
        }
    }

    private static readonly Dictionary<string, Entry> Entries = new();

    public static LevelRewardResult ResolveAttempt(
        string levelId,
        LevelOutcome outcome,
        int deliveredResources,
        int totalOreCapacity)
    {
        string key = string.IsNullOrWhiteSpace(levelId)
            ? "__runtime_level__"
            : levelId;

        Entries.TryGetValue(key, out Entry previous);

        LevelRewardResult result = LevelRewardCalculator.Calculate(
            key,
            outcome,
            deliveredResources,
            totalOreCapacity,
            previous.HasCompleted,
            previous.BestDelivered);

        if (result.AttemptSucceeded)
        {
            Entries[key] = new Entry(
                hasCompleted: true,
                bestDelivered: result.BestDelivered);
        }

        return result;
    }

    public static void Clear()
    {
        Entries.Clear();
    }
}
