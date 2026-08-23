using System.Collections.Generic;
using UnityEngine;

public static class DwarfVisibilitySystem
{
    private static readonly List<DwarfAgent> dwarves = new();

    private static SliceAxis axis;
    private static int directionSign;

    private static int peelDepth;

    public static void Register(DwarfAgent dwarf)
    {
        if (!dwarves.Contains(dwarf))
            dwarves.Add(dwarf);
    }

    public static void Unregister(DwarfAgent dwarf)
    {
        dwarves.Remove(dwarf);
    }

    public static void SetView(SliceAxis newAxis, int sign)
    {
        axis = newAxis;
        directionSign = sign;

        Refresh();
    }

    public static void ChangeLayer(int delta)
    {
        peelDepth = Mathf.Max(0, peelDepth + delta);

        Refresh();
    }

    public static void Reset()
    {
        peelDepth = 0;
        Refresh();
    }

    public static void Refresh()
    {
        foreach (DwarfAgent dwarf in dwarves)
        {
            if (!dwarf.IsActive)
                continue;

            bool visible = IsVisible(dwarf.CurrentVoxel);

            dwarf.SetVisibility(visible);
        }
    }

    private static bool IsVisible(Vector3Int worldPos)
    {
        int coord = GetAxisCoordinate(worldPos);

        // Same logic as VoxelVisibilitySystem
        // front side depends on camera side
        int start = directionSign > 0
            ? int.MaxValue
            : int.MinValue;

        int distance;

        if (directionSign > 0)
            distance = start - coord;
        else
            distance = coord - start;

        // replace infinite distance with actual behaviour
        // by calculating relative to current peel depth
        return Mathf.Abs(coord - GetFrontLayer()) >= peelDepth;
    }

    private static int GetFrontLayer()
    {
        return directionSign > 0
            ? GetMaxCoordinate()
            : GetMinCoordinate();
    }


    // Temporary until we share bounds with VoxelVisibilitySystem
    private static int GetMaxCoordinate()
    {
        return VoxelVisibilitySystem.maxLayer;
    }

    private static int GetMinCoordinate()
    {
        return VoxelVisibilitySystem.minLayer;
    }


    private static int GetAxisCoordinate(Vector3Int pos)
    {
        return axis switch
        {
            SliceAxis.X => pos.x,
            SliceAxis.Z => pos.z,
            _ => pos.x
        };
    }

    public static void RefreshDwarf(DwarfAgent dwarf)
    {
        if (!dwarf.IsActive)
            return;

        bool visible = IsVisible(dwarf.CurrentVoxel);

        dwarf.SetVisibility(visible);
    }
}