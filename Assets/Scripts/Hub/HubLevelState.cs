using System;

public enum HubLevelAvailability
{
    Invalid,
    Locked,
    Available,
    Completed,
    Perfect
}

public sealed class HubLevelState
{
    public string LevelId { get; }
    public string DisplayName { get; }
    public LevelDefinition Level { get; }
    public HubLevelAvailability Availability { get; }
    public int BestDeliveredResources { get; }
    public bool PerfectResourceRun { get; }
    public double BestSimulationSeconds { get; }

    public bool IsValid =>
        Level != null &&
        !string.IsNullOrWhiteSpace(LevelId);

    public bool IsLaunchable =>
        Availability == HubLevelAvailability.Available ||
        Availability == HubLevelAvailability.Completed ||
        Availability == HubLevelAvailability.Perfect;

    public bool IsCompleted =>
        Availability == HubLevelAvailability.Completed ||
        Availability == HubLevelAvailability.Perfect;

    public HubLevelState(
        LevelDefinition level,
        HubLevelAvailability availability,
        CampaignLevelProgressData progress = null)
    {
        Level = level;
        LevelId = level != null &&
                  !string.IsNullOrWhiteSpace(level.LevelId)
            ? level.LevelId.Trim()
            : string.Empty;
        DisplayName = level != null
            ? level.DisplayName
            : string.Empty;
        Availability = availability;
        BestDeliveredResources = Math.Max(
            0,
            progress?.bestDeliveredResources ?? 0);
        PerfectResourceRun =
            progress?.perfectResourceRun ?? false;
        BestSimulationSeconds = Math.Max(
            0d,
            progress?.bestSimulationSeconds ?? 0d);
    }

    public static HubLevelState Invalid(LevelDefinition level = null)
    {
        return new HubLevelState(
            level,
            HubLevelAvailability.Invalid);
    }
}
