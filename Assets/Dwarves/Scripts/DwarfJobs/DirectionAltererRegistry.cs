using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks active Direction Alterer jobs and checks whether a proposed
/// dwarf movement would overlap one.
///
/// Dwarves are redirected before the overlap occurs.
/// </summary>
public static class DirectionAltererRegistry
{
    private static readonly List<DirectionAltererJob> alterers =
        new();

    public static void Register(
        DirectionAltererJob alterer)
    {
        if (alterer == null ||
            alterers.Contains(alterer))
        {
            return;
        }

        alterers.Add(alterer);
    }

    public static void Unregister(
        DirectionAltererJob alterer)
    {
        alterers.Remove(alterer);
    }

    public static bool TryGetRedirect(
        DwarfAgent movingDwarf,
        Vector3Int proposedAnchor,
        out PuzzleSide outputDirection)
    {
        for (int i = alterers.Count - 1;
             i >= 0;
             i--)
        {
            DirectionAltererJob alterer =
                alterers[i];

            if (alterer == null ||
                !alterer.IsOperational)
            {
                alterers.RemoveAt(i);
                continue;
            }

            DwarfAgent altererDwarf =
                alterer.Agent;

            if (altererDwarf == null ||
                altererDwarf == movingDwarf)
            {
                continue;
            }

            // If the dwarves already overlap, redirection cannot solve the
            // overlap. Allow the moving dwarf to continue until it leaves.
            if (VolumesOverlap(
                    movingDwarf.CurrentVoxel,
                    altererDwarf.CurrentVoxel))
            {
                continue;
            } 

            if (!VolumesOverlap(
                    proposedAnchor,
                    altererDwarf.CurrentVoxel))
            {
                continue;
            }

            outputDirection =
                ResolveSafeOutput(
                    movingDwarf,
                    alterer);

            return true;
        }

        outputDirection = default;
        return false;
    }

    private static PuzzleSide ResolveSafeOutput(
        DwarfAgent movingDwarf,
        DirectionAltererJob alterer)
    {
        PuzzleSide requestedDirection =
            alterer.OutputDirection;

        Vector3Int requestedAnchor =
            movingDwarf.CurrentVoxel +
            DirectionUtility.ToVector(
                requestedDirection);

        if (!VolumesOverlap(
                requestedAnchor,
                alterer.Agent.CurrentVoxel))
        {
            return requestedDirection;
        }

        // Temporary safety fallback. Step 9 should prevent the player
        // from choosing an output direction that points into the alterer.
        PuzzleSide fallbackDirection =
            DirectionUtility.Opposite(
                movingDwarf.Facing);

        alterer.ReportInvalidOutputOnce(
            requestedDirection,
            fallbackDirection);

        return fallbackDirection;
    }

    private static bool VolumesOverlap(
        Vector3Int firstAnchor,
        Vector3Int secondAnchor)
    {
        int firstMinX =
            firstAnchor.x +
            DwarfSpatialRules.MinimumLocalX;

        int firstMaxX =
            firstAnchor.x +
            DwarfSpatialRules.MaximumLocalX;

        int firstMinY =
            firstAnchor.y +
            DwarfSpatialRules.MinimumLocalY;

        int firstMaxY =
            firstAnchor.y +
            DwarfSpatialRules.MaximumLocalY;

        int firstMinZ =
            firstAnchor.z +
            DwarfSpatialRules.MinimumLocalZ;

        int firstMaxZ =
            firstAnchor.z +
            DwarfSpatialRules.MaximumLocalZ;

        int secondMinX =
            secondAnchor.x +
            DwarfSpatialRules.MinimumLocalX;

        int secondMaxX =
            secondAnchor.x +
            DwarfSpatialRules.MaximumLocalX;

        int secondMinY =
            secondAnchor.y +
            DwarfSpatialRules.MinimumLocalY;

        int secondMaxY =
            secondAnchor.y +
            DwarfSpatialRules.MaximumLocalY;

        int secondMinZ =
            secondAnchor.z +
            DwarfSpatialRules.MinimumLocalZ;

        int secondMaxZ =
            secondAnchor.z +
            DwarfSpatialRules.MaximumLocalZ;

        bool overlapsX =
            firstMinX <= secondMaxX &&
            firstMaxX >= secondMinX;

        bool overlapsY =
            firstMinY <= secondMaxY &&
            firstMaxY >= secondMinY;

        bool overlapsZ =
            firstMinZ <= secondMaxZ &&
            firstMaxZ >= secondMinZ;

        return
            overlapsX &&
            overlapsY &&
            overlapsZ;
    }
}