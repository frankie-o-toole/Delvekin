using System;

public static class HubLevelStateResolver
{
    public static HubLevelState Resolve(
        HubLevelNode node,
        CampaignProgressService campaign)
    {
        if (node?.Level == null ||
            string.IsNullOrWhiteSpace(node.LevelId))
        {
            return HubLevelState.Invalid(node?.Level);
        }

        CampaignLevelProgressData progress =
            campaign?.GetLevelProgress(node.LevelId);

        if (progress?.completed == true)
        {
            return new HubLevelState(
                node.Level,
                progress.perfectResourceRun
                    ? HubLevelAvailability.Perfect
                    : HubLevelAvailability.Completed,
                progress);
        }

        bool unlocked = node.StartsUnlocked ||
                        PrerequisitesAreSatisfied(node, campaign);

        return new HubLevelState(
            node.Level,
            unlocked
                ? HubLevelAvailability.Available
                : HubLevelAvailability.Locked,
            progress);
    }

    private static bool PrerequisitesAreSatisfied(
        HubLevelNode node,
        CampaignProgressService campaign)
    {
        if (campaign == null ||
            node.Prerequisites == null ||
            node.Prerequisites.Count == 0)
        {
            return false;
        }

        bool requireAll =
            node.UnlockMode ==
            HubLevelUnlockMode.CompleteAllPrerequisites;
        bool foundValidPrerequisite = false;

        foreach (LevelDefinition prerequisite in node.Prerequisites)
        {
            if (prerequisite == null ||
                string.IsNullOrWhiteSpace(prerequisite.LevelId))
            {
                if (requireAll)
                {
                    return false;
                }

                continue;
            }

            foundValidPrerequisite = true;
            bool completed =
                campaign.GetLevelProgress(prerequisite.LevelId)
                    ?.completed == true;

            if (requireAll && !completed)
            {
                return false;
            }

            if (!requireAll && completed)
            {
                return true;
            }
        }

        return requireAll && foundValidPrerequisite;
    }
}
