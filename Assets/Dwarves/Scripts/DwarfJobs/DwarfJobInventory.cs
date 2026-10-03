using System;
using System.Collections.Generic;
using UnityEngine;

public class DwarfJobInventory : MonoBehaviour
{
    [Serializable]
    private class JobStock
    {
        public DwarfJobType type;
        public int availableCount;
    }

    [SerializeField]
    private List<JobStock> startingStock =
        new();

    private readonly Dictionary<DwarfJobType, int> counts =
        new();

    private readonly Dictionary<DwarfJobType, int>
        levelStartingCounts = new();

    private bool usesLevelRules;

    public event Action<DwarfJobType, int> CountChanged;

    private void Awake()
    {
        RebuildInventory();
    }

    public int GetCount(
        DwarfJobType type)
    {
        return counts.TryGetValue(
            type,
            out int count)
                ? count
                : 0;
    }

    public bool HasAvailable(
        DwarfJobType type)
    {
        return GetCount(type) > 0;
    }

    public bool TryConsume(
        DwarfJobType type)
    {
        if (!counts.TryGetValue(
                type,
                out int count) ||
            count <= 0)
        {
            return false;
        }

        count--;

        counts[type] = count;

        CountChanged?.Invoke(
            type,
            count);

        return true;
    }

    public void Refund(
        DwarfJobType type)
    {
        if (type == DwarfJobType.None)
        {
            return;
        }

        int maximum = int.MaxValue;

        if (usesLevelRules &&
            !levelStartingCounts.TryGetValue(type, out maximum))
        {
            maximum = 0;
        }

        int newCount = Mathf.Min(
            GetCount(type) + 1,
            maximum);

        counts[type] =
            newCount;

        CountChanged?.Invoke(
            type,
            newCount);
    }

    public void ResetToStartingStock()
    {
        RebuildInventory();

        foreach (var pair in counts)
        {
            CountChanged?.Invoke(
                pair.Key,
                pair.Value);
        }
    }

    public void ConfigureForLevel(
        IReadOnlyList<LevelJobRule> rules)
    {
        usesLevelRules = true;
        levelStartingCounts.Clear();

        if (rules != null)
        {
            foreach (LevelJobRule rule in rules)
            {
                if (rule == null ||
                    rule.jobType == DwarfJobType.None)
                {
                    continue;
                }

                levelStartingCounts[rule.jobType] =
                    Mathf.Max(0, rule.defaultCount);
            }
        }

        ResetToStartingStock();
    }

    public List<LevelJobRule> CreateStartingRules()
    {
        List<LevelJobRule> result = new();

        if (usesLevelRules)
        {
            foreach (var pair in levelStartingCounts)
            {
                result.Add(
                    new LevelJobRule
                    {
                        jobType = pair.Key,
                        defaultCount = pair.Value,
                        maximumCount = pair.Value
                    });
            }

            return result;
        }

        Dictionary<DwarfJobType, int> totals = new();

        foreach (JobStock stock in startingStock)
        {
            if (stock == null || stock.type == DwarfJobType.None)
            {
                continue;
            }

            totals.TryGetValue(stock.type, out int current);
            totals[stock.type] =
                current + Mathf.Max(0, stock.availableCount);
        }

        foreach (var pair in totals)
        {
            result.Add(
                new LevelJobRule
                {
                    jobType = pair.Key,
                    defaultCount = pair.Value,
                    maximumCount = pair.Value
                });
        }

        return result;
    }

    private void RebuildInventory()
    {
        counts.Clear();

        if (usesLevelRules)
        {
            foreach (var pair in levelStartingCounts)
            {
                counts[pair.Key] = pair.Value;
            }

            return;
        }

        foreach (JobStock stock in startingStock)
        {
            if (stock == null ||
                stock.type == DwarfJobType.None)
            {
                continue;
            }

            int amount =
                Mathf.Max(
                    0,
                    stock.availableCount);

            if (counts.ContainsKey(stock.type))
            {
                counts[stock.type] += amount;
            }
            else
            {
                counts.Add(
                    stock.type,
                    amount);
            }
        }
    }
}
