using System;
using UnityEngine;

[Serializable]
public sealed class LevelJobRule
{
    public DwarfJobType jobType = DwarfJobType.None;

    [Min(0)]
    public int defaultCount;

    [Min(0)]
    public int maximumCount;

    public LevelJobRule Clone()
    {
        return new LevelJobRule
        {
            jobType = jobType,
            defaultCount = defaultCount,
            maximumCount = maximumCount
        };
    }

    public void EnsureValid()
    {
        defaultCount = Mathf.Max(0, defaultCount);
        maximumCount = Mathf.Max(defaultCount, maximumCount);
    }
}
