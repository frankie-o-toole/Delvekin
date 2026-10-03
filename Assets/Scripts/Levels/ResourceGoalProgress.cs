using System;

public readonly struct ResourceGoalProgress
{
    public int Mined { get; }
    public int ActiveDwarves { get; }
    public int UnspawnedDwarves { get; }
    public int Required { get; }

    public int MaximumPossibleMinedResources =>
        Mined + ActiveDwarves + UnspawnedDwarves;

    public bool TargetReached =>
        Mined >= Required;

    public bool IsImpossible =>
        !TargetReached &&
        MaximumPossibleMinedResources < Required;

    public ResourceGoalProgress(
        int mined,
        int activeDwarves,
        int unspawnedDwarves,
        int required)
    {
        Mined = Math.Max(0, mined);
        ActiveDwarves = Math.Max(0, activeDwarves);
        UnspawnedDwarves = Math.Max(0, unspawnedDwarves);
        Required = Math.Max(1, required);
    }
}
