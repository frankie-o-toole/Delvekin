using System;

public sealed class HubLevelPresentation
{
    public HubLevelState State { get; }
    public LevelDefinition Level => State?.Level;
    public string LevelId => State?.LevelId ?? string.Empty;
    public string DisplayName => State?.DisplayName ?? string.Empty;
    public HubLevelAvailability Availability =>
        State?.Availability ?? HubLevelAvailability.Invalid;
    public bool IsLaunchable => State?.IsLaunchable == true;
    public bool IsCompleted => State?.IsCompleted == true;
    public int TotalDwarves { get; }
    public int RequiredMinedResources { get; }
    public int TotalOreCapacity { get; }
    public int BestDeliveredResources =>
        State?.BestDeliveredResources ?? 0;
    public bool PerfectResourceRun =>
        State?.PerfectResourceRun == true;
    public double BestSimulationSeconds =>
        State?.BestSimulationSeconds ?? 0d;
    public int RemainingRewardPotential { get; }
    public string LockedReason { get; }

    public string StatusLabel => Availability switch
    {
        HubLevelAvailability.Locked => "Locked",
        HubLevelAvailability.Available => "Available",
        HubLevelAvailability.Completed => "Completed",
        HubLevelAvailability.Perfect => "Perfect",
        _ => "Unavailable"
    };

    public HubLevelPresentation(
        HubLevelState state,
        string lockedReason)
    {
        State = state;
        TotalDwarves = Math.Max(0, Level?.TotalDwarves ?? 0);
        RequiredMinedResources = Math.Max(
            0,
            Level?.RequiredMinedResources ?? 0);
        TotalOreCapacity = Math.Max(0, Level?.TotalOreCapacity ?? 0);
        RemainingRewardPotential = Math.Max(
            0,
            TotalOreCapacity - BestDeliveredResources);
        LockedReason = lockedReason ?? string.Empty;
    }
}
