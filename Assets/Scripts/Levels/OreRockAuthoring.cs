using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Delvekin/Ore Rock Authoring")]
public sealed class OreRockAuthoring : MonoBehaviour
{
    private static readonly HashSet<OreRockAuthoring> RuntimeRocks =
        new();

    [SerializeField]
    [HideInInspector]
    private string entityId;

    [SerializeField]
    [HideInInspector]
    private LevelDefinition authoringDefinition;

    [Header("Visual")]
    [SerializeField]
    private GameObject visualPrefab;

    [SerializeField]
    private Vector3Int authoringSize = new(6, 8, 6);

    [Header("Ore")]
    [Min(1)]
    [SerializeField]
    private int capacity = 60;

    [Tooltip("Reserved for the future minecart departure direction.")]
    [SerializeField]
    private PuzzleSide facing = PuzzleSide.North;

    [SerializeField]
    [HideInInspector]
    private VoxelWorld voxelWorld;

    [SerializeField]
    [HideInInspector]
    private bool runtimeCopy;

    private GameObject visualInstance;
    private int synchronizedStateHash = int.MinValue;
    private readonly HashSet<DwarfAgent> reservations = new();
    private int extracted;

    public string EntityId => entityId;
    public LevelDefinition AuthoringDefinition => authoringDefinition;
    public GameObject VisualPrefab => visualPrefab;
    public Vector3Int AuthoringSize => authoringSize;
    public int Capacity => Mathf.Max(1, capacity);
    public int Reserved => reservations.Count;
    public int Extracted => extracted;
    public int Remaining =>
        Mathf.Max(0, Capacity - Reserved - Extracted);
    public PuzzleSide Facing => facing;

    public event Action<OreRockAuthoring, DwarfAgent> OreExtracted;

    public static event Action RuntimeOreChanged;

    public static OreRuntimeProgress GetRuntimeProgress()
    {
        int totalCapacity = 0;
        int extractedTotal = 0;
        int reservedTotal = 0;
        int remainingTotal = 0;

        foreach (OreRockAuthoring oreRock in RuntimeRocks)
        {
            if (oreRock == null || !oreRock.isActiveAndEnabled)
            {
                continue;
            }

            totalCapacity += oreRock.Capacity;
            extractedTotal += oreRock.Extracted;
            reservedTotal += oreRock.Reserved;
            remainingTotal += oreRock.Remaining;
        }

        return new OreRuntimeProgress(
            totalCapacity,
            extractedTotal,
            reservedTotal,
            remainingTotal);
    }

    public void ConfigureIdentity(
        string newEntityId,
        LevelDefinition ownerDefinition = null)
    {
        entityId = string.IsNullOrWhiteSpace(newEntityId)
            ? Guid.NewGuid().ToString("N")
            : newEntityId;

        authoringDefinition = ownerDefinition;
    }

    public void Configure(
        VoxelWorld world,
        Vector3 position,
        Quaternion rotation,
        Vector3Int size,
        PuzzleSide departureFacing,
        int oreCapacity,
        GameObject configuredVisualPrefab,
        bool isRuntimeCopy)
    {
        voxelWorld = world;
        authoringSize = ClampSize(size);
        facing = departureFacing;
        capacity = Mathf.Max(1, oreCapacity);
        visualPrefab = configuredVisualPrefab;
        runtimeCopy = isRuntimeCopy;

        transform.SetPositionAndRotation(
            LevelEntityRecord.SnapVolumeCentreToVoxelGrid(
                position,
                authoringSize),
            rotation);
        reservations.Clear();
        extracted = 0;
        RebuildVisual();
        synchronizedStateHash = CalculateStateHash();
    }

    public static bool TryFindAtLeadingFace(
        DwarfAgent dwarf,
        out OreRockAuthoring oreRock)
    {
        oreRock = null;

        if (dwarf == null || !dwarf.IsActive)
        {
            return false;
        }

        Vector3Int direction =
            DirectionUtility.ToVector(dwarf.Facing);

        Vector3Int candidateAnchor =
            dwarf.CurrentVoxel + direction;

        float nearestDistance = float.PositiveInfinity;

        foreach (OreRockAuthoring candidate in RuntimeRocks)
        {
            if (candidate == null || !candidate.isActiveAndEnabled)
            {
                continue;
            }

            bool touches = false;

            foreach (Vector3Int voxel in
                     DwarfSpatialRules.GetLeadingFaceVoxels(
                         candidateAnchor,
                         direction))
            {
                if (!candidate.ContainsVoxel(voxel))
                {
                    continue;
                }

                touches = true;
                break;
            }

            if (!touches)
            {
                continue;
            }

            float distance =
                (candidate.transform.position - dwarf.transform.position)
                .sqrMagnitude;

            if (distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
            oreRock = candidate;
        }

        return oreRock != null;
    }

    public bool TryReserve(DwarfAgent dwarf)
    {
        if (!runtimeCopy ||
            dwarf == null ||
            !dwarf.IsActive ||
            reservations.Contains(dwarf) ||
            Remaining <= 0)
        {
            return false;
        }

        reservations.Add(dwarf);
        RuntimeOreChanged?.Invoke();
        return true;
    }

    public bool CompleteExtraction(DwarfAgent dwarf)
    {
        if (dwarf == null || !reservations.Remove(dwarf))
        {
            return false;
        }

        extracted++;
        OreExtracted?.Invoke(this, dwarf);
        RuntimeOreChanged?.Invoke();
        return true;
    }

    public void CancelReservation(DwarfAgent dwarf)
    {
        if (dwarf != null)
        {
            if (reservations.Remove(dwarf))
            {
                RuntimeOreChanged?.Invoke();
            }
        }
    }

    public LevelEntityRecord CreateEntityRecord()
    {
        EnsureIdentity();
        return LevelEntityRecord.FromOreRock(this);
    }

    public void RefreshVisual()
    {
        RebuildVisual();
    }

    private void EnsureIdentity()
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            entityId = Guid.NewGuid().ToString("N");
        }
    }

    private void RebuildVisual()
    {
        ClearGeneratedVisuals();

        if (visualPrefab != null)
        {
            visualInstance = Instantiate(visualPrefab, transform);
            visualInstance.name = "Visual";
            visualInstance.transform.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            return;
        }

        visualInstance = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visualInstance.name = "Placeholder Visual";
        visualInstance.transform.SetParent(transform, false);
        visualInstance.transform.localScale = authoringSize;

        Collider placeholderCollider =
            visualInstance.GetComponent<Collider>();

        if (placeholderCollider != null)
        {
            placeholderCollider.enabled = false;
        }

    }

    private void ClearGeneratedVisuals()
    {
        for (int index = transform.childCount - 1;
             index >= 0;
             index--)
        {
            Transform child = transform.GetChild(index);

            if (child.name != "Visual" &&
                child.name != "Placeholder Visual")
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        visualInstance = null;
    }

    private void OnValidate()
    {
        EnsureIdentity();
        authoringSize = ClampSize(authoringSize);
        capacity = Mathf.Max(1, capacity);
        synchronizedStateHash = int.MinValue;
    }

    private void OnEnable()
    {
        if (runtimeCopy)
        {
            RuntimeRocks.Add(this);
            RuntimeOreChanged?.Invoke();
        }
    }

    private void OnDisable()
    {
        bool removed = RuntimeRocks.Remove(this);
        reservations.Clear();

        if (removed)
        {
            RuntimeOreChanged?.Invoke();
        }
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Application.isPlaying ||
            runtimeCopy ||
            !gameObject.scene.isLoaded)
        {
            return;
        }

        Vector3 snappedPosition =
            LevelEntityRecord.SnapVolumeCentreToVoxelGrid(
                transform.position,
                authoringSize);

        if ((transform.position - snappedPosition).sqrMagnitude >
            0.000001f)
        {
            transform.position = snappedPosition;
        }

        int currentHash = CalculateStateHash();

        if (currentHash == synchronizedStateHash)
        {
            return;
        }

        synchronizedStateHash = currentHash;

        LevelAuthoringRoot root =
            GetComponentInParent<LevelAuthoringRoot>();

        if (root != null && root.SynchronizeOreRock(this))
        {
            UnityEditor.EditorUtility.SetDirty(root.Definition);
        }
#endif
    }

    private int CalculateStateHash()
    {
        unchecked
        {
            int hash = transform.position.GetHashCode();
            hash = hash * 31 + transform.rotation.GetHashCode();
            hash = hash * 31 + authoringSize.GetHashCode();
            hash = hash * 31 + capacity;
            hash = hash * 31 + facing.GetHashCode();
            hash = hash * 31 +
                (visualPrefab != null
                    ? visualPrefab.GetInstanceID()
                    : 0);
            return hash;
        }
    }

    private void OnDestroy()
    {
        bool removedRuntimeRock = RuntimeRocks.Remove(this);

        if (removedRuntimeRock)
        {
            RuntimeOreChanged?.Invoke();
        }

#if UNITY_EDITOR
        if (Application.isPlaying ||
            runtimeCopy ||
            !gameObject.scene.isLoaded ||
            string.IsNullOrWhiteSpace(entityId))
        {
            return;
        }

        LevelAuthoringRoot root =
            GetComponentInParent<LevelAuthoringRoot>();

        if (root == null ||
            root.Definition == null ||
            root.IsRebuildingAuthoringEntities)
        {
            return;
        }

        UnityEditor.Undo.RecordObject(
            root.Definition,
            "Delete Ore Rock");

        if (root.RemoveAuthoringEntity(entityId))
        {
            UnityEditor.EditorUtility.SetDirty(root.Definition);
        }
#endif
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.95f, 0.55f, 0.15f, 0.9f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, authoringSize);

        Vector3 direction = DirectionUtility.ToVector(facing);
        Gizmos.DrawLine(
            Vector3.zero,
            transform.InverseTransformDirection(direction) * 4f);
    }

    private static Vector3Int ClampSize(Vector3Int value)
    {
        return new Vector3Int(
            Mathf.Max(1, value.x),
            Mathf.Max(1, value.y),
            Mathf.Max(1, value.z));
    }

    private bool ContainsVoxel(Vector3Int voxel)
    {
        Vector3Int minimum = Vector3Int.FloorToInt(
            transform.position - (Vector3)authoringSize * 0.5f +
            Vector3.one * 0.001f);

        Vector3Int maximumExclusive = minimum + authoringSize;

        return
            voxel.x >= minimum.x &&
            voxel.y >= minimum.y &&
            voxel.z >= minimum.z &&
            voxel.x < maximumExclusive.x &&
            voxel.y < maximumExclusive.y &&
            voxel.z < maximumExclusive.z;
    }
}
