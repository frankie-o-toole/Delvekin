using System;

public static class LevelRewardCalculator
{
    public static LevelRewardResult Calculate(
        string levelId,
        LevelOutcome outcome,
        int deliveredResources,
        int totalOreCapacity,
        bool hasPreviousCompletion,
        int previousBestDelivered)
    {
        int capacity = Math.Max(0, totalOreCapacity);
        int delivered = Math.Min(
            capacity,
            Math.Max(0, deliveredResources));
        int previousBest = Math.Min(
            capacity,
            Math.Max(0, previousBestDelivered));

        bool succeeded = outcome == LevelOutcome.Success;
        bool firstCompletion = succeeded && !hasPreviousCompletion;
        int best = succeeded
            ? Math.Max(previousBest, delivered)
            : previousBest;
        int newlyEarned = succeeded
            ? Math.Max(0, delivered - previousBest)
            : 0;

        return new LevelRewardResult(
            levelId,
            succeeded,
            firstCompletion,
            delivered,
            previousBest,
            best,
            newlyEarned,
            capacity);
    }
}
