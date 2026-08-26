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

        int newCount =
            GetCount(type) + 1;

        counts[type] =
            newCount;

        CountChanged?.Invoke(
            type,
            newCount);
    }

    private void RebuildInventory()
    {
        counts.Clear();

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