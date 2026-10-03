using System;

public readonly struct OreRuntimeProgress
{
    public int TotalCapacity { get; }
    public int Extracted { get; }
    public int Reserved { get; }
    public int Remaining { get; }

    public int Accounted =>
        Extracted + Reserved + Remaining;

    public bool IsConsistent =>
        Accounted == TotalCapacity;

    public OreRuntimeProgress(
        int totalCapacity,
        int extracted,
        int reserved,
        int remaining)
    {
        TotalCapacity = Math.Max(0, totalCapacity);
        Extracted = Math.Max(0, extracted);
        Reserved = Math.Max(0, reserved);
        Remaining = Math.Max(0, remaining);
    }
}
