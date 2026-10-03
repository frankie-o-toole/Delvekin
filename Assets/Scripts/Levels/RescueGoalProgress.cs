using System;

public readonly struct RescueGoalProgress
{
    public int Rescued { get; }
    public int Active { get; }
    public int Unspawned { get; }
    public int Required { get; }

    public int MaximumPossibleRescues =>
        Rescued + Active + Unspawned;

    public bool TargetReached =>
        Rescued >= Required;

    public bool IsImpossible =>
        !TargetReached &&
        MaximumPossibleRescues < Required;

    public RescueGoalProgress(
        int rescued,
        int active,
        int unspawned,
        int required)
    {
        Rescued = Math.Max(0, rescued);
        Active = Math.Max(0, active);
        Unspawned = Math.Max(0, unspawned);
        Required = Math.Max(1, required);
    }
}
