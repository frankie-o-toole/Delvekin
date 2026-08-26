using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls dwarf visibility using the same slicing rules as voxels.
///
/// A dwarf remains completely visible while at least one voxel in its
/// 3x5x3 occupied volume is visible. The model itself is never clipped
/// into separate pieces.
/// </summary>
public static class DwarfVisibilitySystem
{
    private static readonly List<DwarfAgent> dwarves =
        new();

    private static bool puzzleVisibilityActive;

    public static void Register(DwarfAgent dwarf)
    {
        if (dwarf == null ||
            dwarves.Contains(dwarf))
        {
            return;
        }

        dwarves.Add(dwarf);
    }

    public static void Unregister(DwarfAgent dwarf)
    {
        dwarves.Remove(dwarf);
    }

    /// <summary>
    /// Enables Puzzle-mode slicing for dwarves.
    ///
    /// The actual axis, direction and peel depth remain owned by
    /// VoxelVisibilitySystem.
    /// </summary>
    public static void SetView(
        SliceAxis newAxis,
        int sign)
    {
        puzzleVisibilityActive = true;
        Refresh();
    }

    /// <summary>
    /// Called after VoxelVisibilitySystem changes its peel depth.
    /// </summary>
    public static void ChangeLayer(int delta)
    {
        Refresh();
    }

    /// <summary>
    /// Refreshes dwarves after the voxel peel depth returns to zero.
    /// </summary>
    public static void Reset()
    {
        Refresh();
    }

    /// <summary>
    /// Disables Puzzle slicing and shows every active dwarf.
    /// </summary>
    public static void ShowAll()
    {
        puzzleVisibilityActive = false;
        Refresh();
    }

    public static void Refresh()
    {
        for (int i = dwarves.Count - 1;
             i >= 0;
             i--)
        {
            DwarfAgent dwarf =
                dwarves[i];

            if (dwarf == null)
            {
                dwarves.RemoveAt(i);
                continue;
            }

            RefreshDwarf(dwarf);
        }
    }

    public static void RefreshDwarf(
        DwarfAgent dwarf)
    {
        if (dwarf == null)
        {
            return;
        }

        if (!dwarf.IsActive)
        {
            dwarf.SetVisibility(false);
            return;
        }

        if (!puzzleVisibilityActive)
        {
            dwarf.SetVisibility(true);
            return;
        }

        bool visible =
            IsAnyOccupiedVoxelVisible(dwarf);

        dwarf.SetVisibility(visible);
    }

    private static bool IsAnyOccupiedVoxelVisible(
        DwarfAgent dwarf)
    {
        foreach (Vector3Int occupiedVoxel
                 in DwarfSpatialRules.GetOccupiedVoxels(
                     dwarf.CurrentVoxel))
        {
            if (VoxelVisibilitySystem.IsVoxelVisible(
                    occupiedVoxel))
            {
                return true;
            }
        }

        return false;
    }
}