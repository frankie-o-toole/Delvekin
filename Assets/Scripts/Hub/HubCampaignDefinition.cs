using System;
using System.Collections.Generic;
using UnityEngine;

public enum HubLevelUnlockMode
{
    CompleteAllPrerequisites,
    CompleteAnyPrerequisite
}

[Serializable]
public sealed class HubLevelNode
{
    [SerializeField]
    private LevelDefinition level;

    [Tooltip(
        "Makes this level available before any prerequisite is completed.")]
    [SerializeField]
    private bool startsUnlocked;

    [SerializeField]
    private HubLevelUnlockMode unlockMode =
        HubLevelUnlockMode.CompleteAllPrerequisites;

    [SerializeField]
    private List<LevelDefinition> prerequisites = new();

    public LevelDefinition Level => level;
    public string LevelId => level != null
        ? level.LevelId
        : string.Empty;
    public bool StartsUnlocked => startsUnlocked;
    public HubLevelUnlockMode UnlockMode => unlockMode;
    public IReadOnlyList<LevelDefinition> Prerequisites =>
        (IReadOnlyList<LevelDefinition>)prerequisites ??
        Array.Empty<LevelDefinition>();

    public HubLevelNode()
    {
    }

    public HubLevelNode(
        LevelDefinition level,
        bool startsUnlocked,
        HubLevelUnlockMode unlockMode,
        IEnumerable<LevelDefinition> prerequisites = null)
    {
        this.level = level;
        this.startsUnlocked = startsUnlocked;
        this.unlockMode = unlockMode;
        this.prerequisites = prerequisites != null
            ? new List<LevelDefinition>(prerequisites)
            : new List<LevelDefinition>();
    }
}

[CreateAssetMenu(
    fileName = "HubCampaignDefinition",
    menuName = "Delvekin/Hub Campaign Definition")]
public sealed class HubCampaignDefinition : ScriptableObject
{
    [SerializeField]
    private List<HubLevelNode> levels = new();

    public IReadOnlyList<HubLevelNode> Levels =>
        (IReadOnlyList<HubLevelNode>)levels ??
        Array.Empty<HubLevelNode>();

    public bool TryGetNode(
        string levelId,
        out HubLevelNode node)
    {
        node = null;

        if (string.IsNullOrWhiteSpace(levelId))
        {
            return false;
        }

        string normalizedId = levelId.Trim();

        foreach (HubLevelNode candidate in Levels)
        {
            if (candidate?.Level != null &&
                string.Equals(
                    candidate.LevelId,
                    normalizedId,
                    StringComparison.Ordinal))
            {
                node = candidate;
                return true;
            }
        }

        return false;
    }

    public void ReplaceLevels(IEnumerable<HubLevelNode> replacement)
    {
        levels = replacement != null
            ? new List<HubLevelNode>(replacement)
            : new List<HubLevelNode>();
    }

    public IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors = new();
        HashSet<string> ids = new(StringComparer.Ordinal);

        for (int index = 0; index < Levels.Count; index++)
        {
            HubLevelNode node = Levels[index];

            if (node?.Level == null)
            {
                errors.Add($"Level node {index} has no LevelDefinition.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.LevelId))
            {
                errors.Add(
                    $"'{node.Level.name}' has no stable LevelId.");
                continue;
            }

            if (!ids.Add(node.LevelId.Trim()))
            {
                errors.Add(
                    $"LevelId '{node.LevelId}' occurs more than once.");
            }

            if (!node.StartsUnlocked &&
                node.Prerequisites.Count == 0)
            {
                errors.Add(
                    $"Level '{node.Level.DisplayName}' is unreachable: " +
                    "it is locked and has no prerequisites.");
            }
        }

        return errors;
    }

    private void OnValidate()
    {
        levels ??= new List<HubLevelNode>();
    }
}
