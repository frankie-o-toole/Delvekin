using System;

public readonly struct ResourceGoalProgress
{
    public int Mined { get; }
    public int ActiveDwarves { get; }
    public int UnspawnedDwarves { get; }
    public int Required { get; }
    public OreRuntimeProgress Ore { get; }

    public int MaximumPossibleMinedResources =>
        Mined + Math.Min(
            ActiveDwarves + UnspawnedDwarves,
            Ore.Reserved + Ore.Remaining);

    public bool TargetReached =>
        Mined >= Required;

    public bool AllResourcesDelivered =>
        Ore.TotalCapacity > 0 &&
        Mined >= Ore.TotalCapacity;

    public bool IsImpossible =>
        !TargetReached &&
        MaximumPossibleMinedResources < Required;

    public ResourceGoalProgress(
        int activeDwarves,
        int unspawnedDwarves,
        int required,
        OreRuntimeProgress ore)
    {
        Mined = Math.Max(0, ore.Extracted);
        ActiveDwarves = Math.Max(0, activeDwarves);
        UnspawnedDwarves = Math.Max(0, unspawnedDwarves);
        Required = Math.Max(1, required);
        Ore = ore;
    }
}
