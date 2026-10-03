using System;
using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Delvekin/Ore Rock Authoring")]
public sealed class OreRockAuthoring : MonoBehaviour
{
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

    public string EntityId => entityId;
    public LevelDefinition AuthoringDefinition => authoringDefinition;
    public GameObject VisualPrefab => visualPrefab;
    public Vector3Int AuthoringSize => authoringSize;
    public int Capacity => Mathf.Max(1, capacity);
    public PuzzleSide Facing => facing;

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

        transform.SetPositionAndRotation(position, rotation);
        RebuildVisual();
        synchronizedStateHash = CalculateStateHash();
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

    private void Update()
    {
#if UNITY_EDITOR
        if (Application.isPlaying ||
            runtimeCopy ||
            !gameObject.scene.isLoaded)
        {
            return;
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
}
