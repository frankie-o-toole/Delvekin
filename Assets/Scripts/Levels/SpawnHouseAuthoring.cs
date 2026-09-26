using System;
using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Delvekin/Spawn House Authoring")]
public sealed class SpawnHouseAuthoring : MonoBehaviour
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
    private Vector3Int authoringSize = new(5, 4, 5);

    [Header("Spawn")]
    [SerializeField]
    private Vector3 spawnMarkerLocalPosition =
        new(0f, -1f, 3f);

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
    public Vector3 SpawnMarkerLocalPosition =>
        spawnMarkerLocalPosition;
    public PuzzleSide Facing => facing;

    public bool IsSpawnMarkerInsideWorld =>
        authoringDefinition != null
            ? authoringDefinition.ContainsWorldPosition(SpawnVoxel)
            : voxelWorld == null ||
              voxelWorld.ContainsExistingChunkAt(SpawnVoxel);

    public bool IsSpawnMarkerInsideGameplayBounds =>
        authoringDefinition != null
            ? authoringDefinition.ContainsGameplayPosition(SpawnVoxel)
            : voxelWorld == null ||
              voxelWorld.ContainsGameplayPosition(SpawnVoxel);

    public bool IsSpawnMarkerValid =>
        IsSpawnMarkerInsideWorld &&
        IsSpawnMarkerInsideGameplayBounds;

    public Vector3Int SpawnVoxel =>
        Vector3Int.FloorToInt(
            transform.TransformPoint(
                spawnMarkerLocalPosition));

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
        Vector3 localSpawnMarker,
        PuzzleSide spawnFacing,
        GameObject configuredVisualPrefab,
        bool isRuntimeCopy)
    {
        voxelWorld = world;
        authoringSize = ClampSize(size);
        spawnMarkerLocalPosition = localSpawnMarker;
        facing = spawnFacing;
        visualPrefab = configuredVisualPrefab;
        runtimeCopy = isRuntimeCopy;

        transform.SetPositionAndRotation(position, rotation);
        RebuildVisual();

        synchronizedStateHash = CalculateStateHash();
    }

    public void SetSpawnMarkerWorldPosition(Vector3 worldPosition)
    {
        Vector3Int voxel = Vector3Int.FloorToInt(worldPosition);
        Vector3 snappedCentre =
            (Vector3)voxel + Vector3.one * 0.5f;

        spawnMarkerLocalPosition =
            transform.InverseTransformPoint(snappedCentre);
    }

    public LevelEntityRecord CreateEntityRecord()
    {
        EnsureIdentity();
        return LevelEntityRecord.FromSpawnHouse(this);
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
            visualInstance = Instantiate(
                visualPrefab,
                transform);

            visualInstance.name = "Visual";
            visualInstance.transform.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            return;
        }

        visualInstance = GameObject.CreatePrimitive(
            PrimitiveType.Cube);

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

            if (visualInstance == child.gameObject)
            {
                visualInstance = null;
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

        if (!runtimeCopy)
        {
            RebuildVisual();
        }

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

        if (root != null &&
            root.SynchronizeSpawnHouse(this))
        {
            UnityEditor.EditorUtility.SetDirty(
                root.Definition);
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
            hash = hash * 31 +
                spawnMarkerLocalPosition.GetHashCode();
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
            "Delete Spawn House");

        if (root.RemoveAuthoringEntity(entityId))
        {
            UnityEditor.EditorUtility.SetDirty(
                root.Definition);
        }
#endif
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.15f, 0.9f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, authoringSize);

        Gizmos.matrix = Matrix4x4.identity;
        Vector3 spawnCentre =
            (Vector3)SpawnVoxel + Vector3.one * 0.5f;

        Gizmos.color = IsSpawnMarkerValid
            ? new Color(0.2f, 1f, 0.25f, 1f)
            : new Color(1f, 0.15f, 0.1f, 1f);
        Gizmos.DrawWireCube(
            spawnCentre,
            Vector3.one * 0.9f);

        Vector3 direction =
            DirectionUtility.ToVector(facing);

        Gizmos.DrawLine(
            spawnCentre,
            spawnCentre + direction * 1.5f);
    }

    private static Vector3Int ClampSize(Vector3Int value)
    {
        return new Vector3Int(
            Mathf.Max(1, value.x),
            Mathf.Max(1, value.y),
            Mathf.Max(1, value.z));
    }
}
