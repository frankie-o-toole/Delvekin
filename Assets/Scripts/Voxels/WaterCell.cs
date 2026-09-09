using System;
using UnityEngine;

public enum WaterAmount : byte
{
    Half = 1,
    Full = 2
}

public enum WaterMotion : byte
{
    Still,
    Flowing
}

/// <summary>
/// Runtime gameplay state for one Water voxel.
/// Amount, motion and routing are intentionally independent: a river can be
/// Flowing for gameplay while its simulation is asleep.
/// </summary>
[Serializable]
public struct WaterCell
{
    public WaterAmount Amount;
    public WaterMotion Motion;
    public Vector3Int PrimaryFlowDirection;
    public Vector3Int SecondaryFlowDirection;
    public byte PrimaryCapacity;
    public byte SecondaryCapacity;

    public WaterCell(
        WaterAmount amount,
        WaterMotion motion = WaterMotion.Still)
    {
        Amount = amount;
        Motion = motion;
        PrimaryFlowDirection = Vector3Int.zero;
        SecondaryFlowDirection = Vector3Int.zero;
        PrimaryCapacity = 0;
        SecondaryCapacity = 0;
    }
}
