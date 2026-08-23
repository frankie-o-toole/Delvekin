using UnityEngine;

/// <summary>
/// Editor visualization for the logical voxel volume occupied by a dwarf.
///
/// This component does not participate in gameplay. It only draws gizmos
/// when the dwarf is selected in the Unity Editor.
/// </summary>
public class DwarfVolumeGizmo : MonoBehaviour
{
    [Header("Visibility")]
    [SerializeField]
    private bool showOccupiedVolume = true;

    [SerializeField]
    private bool showSupportVoxels = true;

    [SerializeField]
    private bool showAnchor = true;

    [SerializeField]
    private bool showLeadingFace = true;

    [Header("Colours")]
    [SerializeField]
    private Color occupiedColour =
        new Color(0f, 0.8f, 1f, 0.8f);

    [SerializeField]
    private Color supportColour =
        new Color(0.2f, 1f, 0.2f, 0.8f);

    [SerializeField]
    private Color anchorColour =
        new Color(1f, 0.9f, 0f, 1f);

    [SerializeField]
    private Color leadingFaceColour =
        new Color(1f, 0.25f, 0.1f, 1f);

    [Header("Display")]
    [SerializeField]
    [Range(0.8f, 1f)]
    private float cellSize = 0.94f;

    [SerializeField]
    private float anchorRadius = 0.15f;

    private void OnDrawGizmosSelected()
    {
        if (showOccupiedVolume)
        {
            DrawOccupiedVolume();
        }

        if (showSupportVoxels)
        {
            DrawSupportVolume();
        }

        if (showAnchor)
        {
            DrawAnchor();
        }

        if (showLeadingFace)
        {
            DrawLeadingFace();
        }
    }

    private void DrawOccupiedVolume()
    {
        Gizmos.color = occupiedColour;

        foreach (Vector3Int offset
                 in DwarfSpatialRules.GetOccupiedOffsets())
        {
            Vector3 cellCentre = GetCellCentre(offset);
            Gizmos.DrawWireCube(
                cellCentre,
                Vector3.one * cellSize);
        }
    }

    private void DrawSupportVolume()
    {
        Gizmos.color = supportColour;

        for (int z = DwarfSpatialRules.MinimumLocalZ;
             z <= DwarfSpatialRules.MaximumLocalZ;
             z++)
        {
            for (int x = DwarfSpatialRules.MinimumLocalX;
                 x <= DwarfSpatialRules.MaximumLocalX;
                 x++)
            {
                Vector3 localCentre =
                    new Vector3(x, -0.5f, z);

                Vector3 worldCentre =
                    transform.TransformPoint(localCentre);

                Gizmos.DrawWireCube(
                    worldCentre,
                    Vector3.one * cellSize);
            }
        }
    }

    private void DrawAnchor()
    {
        Gizmos.color = anchorColour;
        Gizmos.DrawSphere(transform.position, anchorRadius);
    }

    private void DrawLeadingFace()
    {
        Gizmos.color = leadingFaceColour;

        Vector3Int localDirection =
            GetClosestLocalCardinalDirection();

        if (localDirection.x != 0)
        {
            int leadingX = localDirection.x > 0
                ? DwarfSpatialRules.MaximumLocalX
                : DwarfSpatialRules.MinimumLocalX;

            for (int y = DwarfSpatialRules.MinimumLocalY;
                 y <= DwarfSpatialRules.MaximumLocalY;
                 y++)
            {
                for (int z = DwarfSpatialRules.MinimumLocalZ;
                     z <= DwarfSpatialRules.MaximumLocalZ;
                     z++)
                {
                    Vector3 cellCentre = GetCellCentre(
                        new Vector3Int(leadingX, y, z));

                    Gizmos.DrawWireCube(
                        cellCentre,
                        Vector3.one * cellSize);
                }
            }

            return;
        }

        int leadingZ = localDirection.z > 0
            ? DwarfSpatialRules.MaximumLocalZ
            : DwarfSpatialRules.MinimumLocalZ;

        for (int y = DwarfSpatialRules.MinimumLocalY;
             y <= DwarfSpatialRules.MaximumLocalY;
             y++)
        {
            for (int x = DwarfSpatialRules.MinimumLocalX;
                 x <= DwarfSpatialRules.MaximumLocalX;
                 x++)
            {
                Vector3 cellCentre = GetCellCentre(
                    new Vector3Int(x, y, leadingZ));

                Gizmos.DrawWireCube(
                    cellCentre,
                    Vector3.one * cellSize);
            }
        }
    }

    private Vector3 GetCellCentre(Vector3Int localOffset)
    {
        Vector3 localCentre = new Vector3(
            localOffset.x,
            localOffset.y + 0.5f,
            localOffset.z);

        return transform.TransformPoint(localCentre);
    }

    private Vector3Int GetClosestLocalCardinalDirection()
    {
        Vector3 localForward =
            transform.InverseTransformDirection(transform.forward);

        if (Mathf.Abs(localForward.x)
            > Mathf.Abs(localForward.z))
        {
            return localForward.x >= 0f
                ? Vector3Int.right
                : Vector3Int.left;
        }

        return localForward.z >= 0f
            ? Vector3Int.forward
            : Vector3Int.back;
    }
}