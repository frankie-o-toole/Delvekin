using UnityEngine;

public class DwarfAgent : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField]
    private Transform visualRoot;

    [SerializeField]
    private Collider selectionCollider;

    public bool IsActive { get; private set; }

    /// <summary>
    /// Bottom-centre occupied air cell of the dwarf.
    /// </summary>
    public Vector3Int CurrentVoxel { get; private set; }

    public Vector3Int TargetVoxel { get; private set; }

    public PuzzleSide Facing { get; private set; }

    public bool IsFrozen { get; private set; }

    public Transform VisualRoot =>
        visualRoot;

    private Renderer[] renderers;

    private void Awake()
    {
        if (visualRoot == null)
        {
            Transform foundVisualRoot =
                transform.Find("VisualRoot");

            if (foundVisualRoot != null)
            {
                visualRoot = foundVisualRoot;
            }
            else
            {
                Debug.LogError(
                    $"{name} has no VisualRoot assigned.",
                    this);
            }
        }

        if (selectionCollider == null)
        {
            Transform selectionObject =
                transform.Find("SelectionCollider");

            if (selectionObject != null)
            {
                selectionCollider =
                    selectionObject.GetComponent<Collider>();
            }

            if (selectionCollider == null)
            {
                Debug.LogWarning(
                    $"{name} has no selection collider assigned.",
                    this);
            }
        }

        renderers =
            GetComponentsInChildren<Renderer>();

        DwarfVisibilitySystem.Register(this);
    }

    public void SetVisibility(bool visible)
    {
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }

        if (selectionCollider != null)
        {
            selectionCollider.enabled =
                visible && IsActive;
        }
    }

    /// <summary>
    /// Changes logical facing and immediately updates the visual.
    ///
    /// Existing abilities can continue using this overload when an
    /// immediate turn is desired.
    /// </summary>
    public void SetFacing(PuzzleSide facing)
    {
        SetFacing(
            facing,
            snapVisual: true);
    }

    /// <summary>
    /// Changes logical facing, optionally leaving visual rotation to
    /// DwarfMovement for a smooth turn.
    /// </summary>
    public void SetFacing(
        PuzzleSide facing,
        bool snapVisual)
    {
        Facing = facing;

        if (snapVisual)
        {
            SnapVisualToFacing();
        }
    }

    public void SnapVisualToFacing()
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.rotation =
            GetFacingRotation(Facing);
    }

    public Quaternion GetFacingRotation()
    {
        return GetFacingRotation(Facing);
    }

    public static Quaternion GetFacingRotation(
        PuzzleSide facing)
    {
        Vector3Int direction =
            FacingToDirection(facing);

        return Quaternion.LookRotation(
            (Vector3)direction,
            Vector3.up);
    }

    public void Activate(Vector3Int spawnVoxel)
    {
        Activate(
            spawnVoxel,
            PuzzleSide.North);
    }

    public void Activate(
        Vector3Int spawnVoxel,
        PuzzleSide initialFacing)
    {
        CurrentVoxel = spawnVoxel;
        TargetVoxel = spawnVoxel;

        IsFrozen = false;
        IsActive = true;

        // The gameplay root remains world/grid aligned.
        transform.rotation =
            Quaternion.identity;

        transform.position =
            DwarfSpatialRules
                .AnchorVoxelToRootPosition(spawnVoxel);

        SetFacing(
            initialFacing,
            snapVisual: true);

        gameObject.SetActive(true);

        DwarfVisibilitySystem.RefreshDwarf(this);
    }

    public void Deactivate()
    {
        IsActive = false;
        IsFrozen = false;

        CurrentVoxel = default;
        TargetVoxel = default;

        gameObject.SetActive(false);
    }

    public void Tick()
    {
        if (!IsActive || IsFrozen)
        {
            return;
        }
    }

    public void Freeze()
    {
        IsFrozen = true;
    }

    public void Unfreeze()
    {
        IsFrozen = false;
    }

    public void SetCurrentVoxel(Vector3Int voxel)
    {
        CurrentVoxel = voxel;

        transform.position =
            DwarfSpatialRules
                .AnchorVoxelToRootPosition(voxel);

        DwarfVisibilitySystem.RefreshDwarf(this);
    }

    public void SetTargetVoxel(Vector3Int voxel)
    {
        TargetVoxel = voxel;
    }

    private static Vector3Int FacingToDirection(
        PuzzleSide facing)
    {
        switch (facing)
        {
            case PuzzleSide.North:
                return Vector3Int.forward;

            case PuzzleSide.East:
                return Vector3Int.right;

            case PuzzleSide.South:
                return Vector3Int.back;

            case PuzzleSide.West:
                return Vector3Int.left;

            default:
                return Vector3Int.forward;
        }
    }
    private void OnDestroy()
    {
        DwarfVisibilitySystem.Unregister(this);
    }
}