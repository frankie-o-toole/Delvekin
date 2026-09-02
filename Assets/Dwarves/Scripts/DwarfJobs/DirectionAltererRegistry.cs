using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks active Direction Alterer jobs and checks whether a proposed
/// dwarf movement would overlap one.
///
/// Approaching dwarves are redirected before entering the alterer.
/// Dwarves that already partially overlap are still redirected until
/// their anchor reaches the alterer's anchor. Once centred on or past
/// the alterer, they are allowed to continue.
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

            bool currentlyOverlapping =
                VolumesOverlap(
                    movingDwarf.CurrentVoxel,
                    altererDwarf.CurrentVoxel);

            if (currentlyOverlapping)
            {
                /*
                 * A dwarf that has entered only partway should still
                 * be redirected.
                 *
                 * Once its anchor is aligned with or has passed the
                 * Direction Alterer's anchor along its direction of
                 * travel, it may continue forward. Redirecting at that
                 * point would pull it backward through the alterer.
                 */
                if (HasReachedOrPassedAltererCentre(
                        movingDwarf,
                        altererDwarf))
                {
                    continue;
                }

                outputDirection =
                    ResolveSafeOutput(
                        movingDwarf,
                        alterer);

                return true;
            }

            /*
             * The dwarves do not currently overlap. Check whether the
             * moving dwarf's next proposed anchor would enter the
             * Direction Alterer's occupied volume.
             */
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

    /// <summary>
    /// Returns true once the moving dwarf's anchor is aligned with or
    /// has travelled beyond the Direction Alterer's anchor.
    ///
    /// The dot product makes this work for all four horizontal
    /// directions without separate left/right/forward/back rules.
    /// </summary>
    private static bool HasReachedOrPassedAltererCentre(
        DwarfAgent movingDwarf,
        DwarfAgent altererDwarf)
    {
        Vector3Int movementDirection =
            DirectionUtility.ToVector(
                movingDwarf.Facing);

        Vector3Int fromMovingDwarfToAlterer =
            altererDwarf.CurrentVoxel -
            movingDwarf.CurrentVoxel;

        int remainingDistanceAlongMovement =
            fromMovingDwarfToAlterer.x * movementDirection.x +
            fromMovingDwarfToAlterer.y * movementDirection.y +
            fromMovingDwarfToAlterer.z * movementDirection.z;

        return remainingDistanceAlongMovement <= 0;
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

        /*
         * Temporary safety fallback. If the requested output would
         * direct the moving dwarf farther into the alterer, turn it
         * back toward the direction from which it approached.
         */
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