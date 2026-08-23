using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the logical dimensions and voxel-space shape of a dwarf.
///
/// The AnchorVoxel is the bottom-centre air voxel occupied by the dwarf.
///
/// A dwarf occupies:
///     3 voxels wide
///     5 voxels high
///     3 voxels deep
///
/// Relative to the anchor:
///     X: -1 through +1
///     Y:  0 through +4
///     Z: -1 through +1
///
/// The supporting floor is one voxel below the occupied volume:
///     Y: -1
/// </summary>
public static class DwarfSpatialRules
{
    public const int Width = 3;
    public const int Height = 5;
    public const int Depth = 3;

    public const int HalfWidth = Width / 2;
    public const int HalfDepth = Depth / 2;

    public const int MinimumLocalX = -HalfWidth;
    public const int MaximumLocalX = HalfWidth;

    public const int MinimumLocalY = 0;
    public const int MaximumLocalY = Height - 1;

    public const int MinimumLocalZ = -HalfDepth;
    public const int MaximumLocalZ = HalfDepth;

    public const int OccupiedVoxelCount = Width * Height * Depth;
    public const int FootprintVoxelCount = Width * Depth;

    /// <summary>
    /// Returns every local voxel offset occupied by a dwarf.
    /// </summary>
    public static IEnumerable<Vector3Int> GetOccupiedOffsets()
    {
        for (int y = MinimumLocalY; y <= MaximumLocalY; y++)
        {
            for (int z = MinimumLocalZ; z <= MaximumLocalZ; z++)
            {
                for (int x = MinimumLocalX; x <= MaximumLocalX; x++)
                {
                    yield return new Vector3Int(x, y, z);
                }
            }
        }
    }

    /// <summary>
    /// Returns every world voxel occupied by a dwarf at the given anchor.
    /// </summary>
    public static IEnumerable<Vector3Int> GetOccupiedVoxels(
        Vector3Int anchorVoxel)
    {
        foreach (Vector3Int offset in GetOccupiedOffsets())
        {
            yield return anchorVoxel + offset;
        }
    }

    /// <summary>
    /// Returns the nine bottom-layer voxels occupied by the dwarf.
    ///
    /// These are the voxels containing the dwarf's feet and lower body,
    /// not the solid floor beneath it.
    /// </summary>
    public static IEnumerable<Vector3Int> GetFootprintVoxels(
        Vector3Int anchorVoxel)
    {
        for (int z = MinimumLocalZ; z <= MaximumLocalZ; z++)
        {
            for (int x = MinimumLocalX; x <= MaximumLocalX; x++)
            {
                yield return anchorVoxel + new Vector3Int(x, 0, z);
            }
        }
    }

    /// <summary>
    /// Returns the nine terrain voxels immediately underneath the dwarf.
    /// These voxels determine whether the dwarf has sufficient support.
    /// </summary>
    public static IEnumerable<Vector3Int> GetSupportVoxels(
        Vector3Int anchorVoxel)
    {
        for (int z = MinimumLocalZ; z <= MaximumLocalZ; z++)
        {
            for (int x = MinimumLocalX; x <= MaximumLocalX; x++)
            {
                yield return anchorVoxel + new Vector3Int(x, -1, z);
            }
        }
    }

    /// <summary>
    /// Returns the centre support voxel directly beneath the anchor.
    /// </summary>
    public static Vector3Int GetCentreSupportVoxel(
        Vector3Int anchorVoxel)
    {
        return anchorVoxel + Vector3Int.down;
    }

    /// <summary>
    /// Returns the 3x5 face of voxels entering new terrain when moving
    /// toward a candidate anchor.
    ///
    /// candidateAnchor is the proposed new anchor position.
    /// moveDirection must be one of:
    ///     left, right, forward or back
    /// </summary>
    public static IEnumerable<Vector3Int> GetLeadingFaceVoxels(
        Vector3Int candidateAnchor,
        Vector3Int moveDirection)
    {
        ValidateHorizontalDirection(moveDirection);

        if (moveDirection.x != 0)
        {
            int leadingX = moveDirection.x > 0
                ? MaximumLocalX
                : MinimumLocalX;

            for (int y = MinimumLocalY; y <= MaximumLocalY; y++)
            {
                for (int z = MinimumLocalZ; z <= MaximumLocalZ; z++)
                {
                    yield return candidateAnchor
                        + new Vector3Int(leadingX, y, z);
                }
            }

            yield break;
        }

        int leadingZ = moveDirection.z > 0
            ? MaximumLocalZ
            : MinimumLocalZ;

        for (int y = MinimumLocalY; y <= MaximumLocalY; y++)
        {
            for (int x = MinimumLocalX; x <= MaximumLocalX; x++)
            {
                yield return candidateAnchor
                    + new Vector3Int(x, y, leadingZ);
            }
        }
    }

    /// <summary>
    /// Returns the 3x5 face at the back of the dwarf relative to movement.
    /// This may later be useful for debugging, turning, or job effects.
    /// </summary>
    public static IEnumerable<Vector3Int> GetTrailingFaceVoxels(
        Vector3Int anchorVoxel,
        Vector3Int moveDirection)
    {
        ValidateHorizontalDirection(moveDirection);

        if (moveDirection.x != 0)
        {
            int trailingX = moveDirection.x > 0
                ? MinimumLocalX
                : MaximumLocalX;

            for (int y = MinimumLocalY; y <= MaximumLocalY; y++)
            {
                for (int z = MinimumLocalZ; z <= MaximumLocalZ; z++)
                {
                    yield return anchorVoxel
                        + new Vector3Int(trailingX, y, z);
                }
            }

            yield break;
        }

        int trailingZ = moveDirection.z > 0
            ? MinimumLocalZ
            : MaximumLocalZ;

        for (int y = MinimumLocalY; y <= MaximumLocalY; y++)
        {
            for (int x = MinimumLocalX; x <= MaximumLocalX; x++)
            {
                yield return anchorVoxel
                    + new Vector3Int(x, y, trailingZ);
            }
        }
    }

    /// <summary>
    /// Returns whether a voxel is inside the dwarf's occupied volume.
    /// </summary>
    public static bool ContainsVoxel(
        Vector3Int anchorVoxel,
        Vector3Int voxel)
    {
        Vector3Int offset = voxel - anchorVoxel;

        return offset.x >= MinimumLocalX
            && offset.x <= MaximumLocalX
            && offset.y >= MinimumLocalY
            && offset.y <= MaximumLocalY
            && offset.z >= MinimumLocalZ
            && offset.z <= MaximumLocalZ;
    }

    /// <summary>
    /// Converts an anchor voxel into the world-space position of the
    /// dwarf prefab root.
    ///
    /// The prefab root sits at foot level: the bottom face of the
    /// anchor voxel.
    /// </summary>
    public static Vector3 AnchorVoxelToRootPosition(
        Vector3Int anchorVoxel)
    {
        return new Vector3(
            anchorVoxel.x + 0.5f,
            anchorVoxel.y,
            anchorVoxel.z + 0.5f);
    }

    /// <summary>
    /// Converts the foot-level position of a dwarf prefab root back
    /// into its logical anchor voxel.
    /// </summary>
    public static Vector3Int RootPositionToAnchorVoxel(
        Vector3 rootPosition)
    {
        return new Vector3Int(
            Mathf.FloorToInt(rootPosition.x),
            Mathf.FloorToInt(rootPosition.y),
            Mathf.FloorToInt(rootPosition.z));
    }

    /// <summary>
    /// Ensures movement directions used by spatial queries are horizontal,
    /// cardinal and exactly one voxel long.
    /// </summary>
    private static void ValidateHorizontalDirection(
        Vector3Int direction)
    {
        bool isHorizontal =
            direction.y == 0;

        bool isOneVoxelLong =
            Mathf.Abs(direction.x) + Mathf.Abs(direction.z) == 1;

        if (!isHorizontal || !isOneVoxelLong)
        {
            throw new System.ArgumentException(
                $"Direction {direction} is invalid. "
                + "Expected one horizontal cardinal step, such as "
                + "Vector3Int.forward or Vector3Int.right.");
        }
    }
}