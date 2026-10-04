using System;

public static class DwarfJobIdUtility
{
    public const string DirectionAlter = "direction_alter";
    public const string Tunneller = "tunneller";
    public const string Digger = "digger";
    public const string StairBuilder = "stair_builder";
    public const string LadderBuilder = "ladder_builder";

    public static bool TryGetId(
        DwarfJobType jobType,
        out string jobId)
    {
        jobId = jobType switch
        {
            DwarfJobType.DirectionAlter => DirectionAlter,
            DwarfJobType.Tunneller => Tunneller,
            DwarfJobType.Digger => Digger,
            DwarfJobType.StairBuilder => StairBuilder,
            DwarfJobType.LadderBuilder => LadderBuilder,
            _ => string.Empty
        };

        return jobId.Length > 0;
    }

    public static bool TryGetJobType(
        string jobId,
        out DwarfJobType jobType)
    {
        switch (Normalize(jobId))
        {
            case DirectionAlter:
                jobType = DwarfJobType.DirectionAlter;
                return true;
            case Tunneller:
                jobType = DwarfJobType.Tunneller;
                return true;
            case Digger:
                jobType = DwarfJobType.Digger;
                return true;
            case StairBuilder:
                jobType = DwarfJobType.StairBuilder;
                return true;
            case LadderBuilder:
                jobType = DwarfJobType.LadderBuilder;
                return true;
            default:
                jobType = DwarfJobType.None;
                return false;
        }
    }

    public static string Normalize(string jobId)
    {
        return string.IsNullOrWhiteSpace(jobId)
            ? string.Empty
            : jobId.Trim().ToLowerInvariant();
    }
}
