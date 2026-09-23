using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class WaterPortal : MonoBehaviour
{
    [SerializeField]
    [HideInInspector]
    private string entityId;

    [SerializeField]
    [HideInInspector]
    private LevelDefinition authoringDefinition;

    [Header("Portal Volume")]
    [SerializeField]
    private Vector3Int size = Vector3Int.one;

    [SerializeField]
    private PuzzleSide facing = PuzzleSide.North;

    [Header("References")]
    [SerializeField]
    protected VoxelWorld voxelWorld;

    public string EntityId => entityId;
    public LevelDefinition AuthoringDefinition => authoringDefinition;
    public Vector3Int Size => size;
    public PuzzleSide Facing => facing;
    public Vector3Int Direction => DirectionUtility.ToVector(facing);
    public VoxelWorld World => voxelWorld;

    public void ConfigureIdentity(
        string newEntityId,
        LevelDefinition ownerDefinition = null)
    {
        entityId = string.IsNullOrWhiteSpace(newEntityId)
            ? Guid.NewGuid().ToString("N")
            : newEntityId;

        authoringDefinition = ownerDefinition;
    }

    public void EnsureIdentity()
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            entityId = Guid.NewGuid().ToString("N");
        }
    }

    public LevelEntityRecord CreateEntityRecord()
    {
        EnsureIdentity();
        return LevelEntityRecord.FromPortal(this);
    }

    public void Configure(
        VoxelWorld world,
        Vector3Int minimumVoxel,
        Vector3Int newSize,
        PuzzleSide newFacing)
    {
        voxelWorld = world;
        size = ClampSize(newSize);
        facing = newFacing;
        transform.position =
            (Vector3)minimumVoxel + (Vector3)size * 0.5f;
    }

    public void SnapToVoxelGrid()
    {
        size = ClampSize(size);
        Vector3Int minimum = MinimumVoxel;
        transform.position =
            (Vector3)minimum + (Vector3)size * 0.5f;
    }

    public Vector3Int MinimumVoxel
    {
        get
        {
            Vector3 halfSize = (Vector3)size * 0.5f;
            return Vector3Int.FloorToInt(
                transform.position - halfSize + Vector3.one * 0.001f);
        }
    }

    public int CoveredCellCount => size.x * size.y * size.z;

    public void GetCoveredVoxels(List<Vector3Int> results)
    {
        if (results == null)
        {
            return;
        }

        Vector3Int minimum = MinimumVoxel;

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                for (int z = 0; z < size.z; z++)
                {
                    results.Add(
                        minimum + new Vector3Int(x, y, z));
                }
            }
        }
    }

    protected void ResolveWorld()
    {
        if (voxelWorld == null)
        {
            voxelWorld = FindFirstObjectByType<VoxelWorld>();
        }
    }

    protected virtual void OnValidate()
    {
        EnsureIdentity();
        size = ClampSize(size);
        SnapToVoxelGrid();
    }

    private static Vector3Int ClampSize(Vector3Int value)
    {
        return new Vector3Int(
            Mathf.Max(1, value.x),
            Mathf.Max(1, value.y),
            Mathf.Max(1, value.z));
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        if (Application.isPlaying ||
            !gameObject.scene.isLoaded ||
            string.IsNullOrWhiteSpace(entityId))
        {
            return;
        }

        LevelAuthoringRoot root =
            GetComponentInParent<LevelAuthoringRoot>();

        if (root != null &&
            root.RemoveAuthoringEntity(entityId))
        {
            UnityEditor.EditorUtility.SetDirty(root.Definition);
        }
#endif
    }

    protected abstract Color GizmoColor { get; }

    protected virtual void OnDrawGizmos()
    {
        Vector3 drawSize = size;
        Vector3 centre = (Vector3)MinimumVoxel + drawSize * 0.5f;
        Vector3 direction = DirectionUtility.ToVector(facing);

        Gizmos.color = GizmoColor;
        Gizmos.DrawWireCube(centre, drawSize);

        Vector3 arrowStart = centre;
        Vector3 arrowEnd =
            arrowStart + direction * (Mathf.Max(size.x, size.z) * 0.75f + 0.5f);

        Gizmos.DrawLine(arrowStart, arrowEnd);
        Gizmos.DrawSphere(arrowEnd, 0.15f);
    }
}
