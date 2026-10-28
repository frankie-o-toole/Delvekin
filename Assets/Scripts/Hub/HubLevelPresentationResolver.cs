using System;
using System.Collections.Generic;

public static class HubLevelPresentationResolver
{
    public static HubLevelPresentation Resolve(
        HubLevelState state,
        HubLevelNode node,
        CampaignProgressService campaign)
    {
        return new HubLevelPresentation(
            state,
            BuildLockedReason(state, node, campaign));
    }

    private static string BuildLockedReason(
        HubLevelState state,
        HubLevelNode node,
        CampaignProgressService campaign)
    {
        if (state?.Availability != HubLevelAvailability.Locked)
        {
            return string.Empty;
        }

        if (node == null || node.Prerequisites.Count == 0)
        {
            return "No unlock route has been configured.";
        }

        List<string> incompleteNames = new();

        foreach (LevelDefinition prerequisite in node.Prerequisites)
        {
            if (prerequisite == null ||
                string.IsNullOrWhiteSpace(prerequisite.LevelId))
            {
                continue;
            }

            bool completed = campaign
                ?.GetLevelProgress(prerequisite.LevelId)
                ?.completed == true;

            if (!completed)
            {
                incompleteNames.Add(
                    string.IsNullOrWhiteSpace(prerequisite.DisplayName)
                        ? prerequisite.name
                        : prerequisite.DisplayName);
            }
        }

        if (incompleteNames.Count == 0)
        {
            return "Complete the required previous level.";
        }

        string levelNames = string.Join(", ", incompleteNames);

        return node.UnlockMode ==
               HubLevelUnlockMode.CompleteAnyPrerequisite
            ? $"Complete one of: {levelNames}."
            : $"Complete: {levelNames}.";
    }
}
