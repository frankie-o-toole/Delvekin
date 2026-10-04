using System.Collections.Generic;
using UnityEngine;

public sealed class EffectiveJobAvailability
{
    public DwarfJobType JobType { get; }
    public int StartingCount { get; }
    public int MaximumCount { get; }

    public EffectiveJobAvailability(
        DwarfJobType jobType,
        int startingCount,
        int maximumCount)
    {
        JobType = jobType;
        StartingCount = Mathf.Max(0, startingCount);
        MaximumCount = Mathf.Max(StartingCount, maximumCount);
    }
}

public static class EffectiveJobAvailabilityResolver
{
    private static readonly DwarfJobType[] FixedOrder =
    {
        DwarfJobType.DirectionAlter,
        DwarfJobType.Tunneller,
        DwarfJobType.Digger,
        DwarfJobType.StairBuilder,
        DwarfJobType.LadderBuilder
    };

    public static IReadOnlyList<EffectiveJobAvailability> Resolve(
        IReadOnlyList<LevelJobRule> levelRules,
        CampaignProgressService campaign)
    {
        return Resolve(
            levelRules,
            campaign?.GetUnlockedJobs());
    }

    public static IReadOnlyList<EffectiveJobAvailability> Resolve(
        IReadOnlyList<LevelJobRule> levelRules,
        IEnumerable<DwarfJobType> unlockedJobs)
    {
        HashSet<DwarfJobType> unlocked = new();

        if (unlockedJobs != null)
        {
            foreach (DwarfJobType jobType in unlockedJobs)
            {
                if (DwarfJobFactory.IsImplemented(jobType))
                {
                    unlocked.Add(jobType);
                }
            }
        }

        Dictionary<DwarfJobType, LevelJobRule> authored = new();

        if (levelRules != null)
        {
            foreach (LevelJobRule rule in levelRules)
            {
                if (rule == null ||
                    !DwarfJobFactory.IsImplemented(rule.jobType))
                {
                    continue;
                }

                authored[rule.jobType] = rule;
            }
        }

        List<EffectiveJobAvailability> result = new();

        foreach (DwarfJobType jobType in FixedOrder)
        {
            if (!unlocked.Contains(jobType) ||
                !authored.TryGetValue(jobType, out LevelJobRule rule))
            {
                continue;
            }

            result.Add(new EffectiveJobAvailability(
                jobType,
                rule.defaultCount,
                rule.maximumCount));
        }

        return result;
    }
}
