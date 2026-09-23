using System.Collections.Generic;
using UnityEngine;

public enum LevelAuthoringShape
{
    Single,
    Line,
    Box
}

public enum LevelAuthoringAction
{
    Place,
    Erase
}

public enum LevelPrefabPlacementMode
{
    Additive,
    ReplaceVolume
}

public enum LevelPrefabRotation
{
    Degrees0,
    Degrees90,
    Degrees180,
    Degrees270
}

public sealed class LevelAuthoringRoot : MonoBehaviour
{
    [SerializeField]
    private LevelDefinition levelDefinition;

    [Tooltip("VoxelWorld used to render the generated preview and runtime copy.")]
    [SerializeField]
    private VoxelWorld voxelWorld;

    [Header("Scene View Voxel Tool")]
    [SerializeField]
    private bool voxelToolEnabled = true;

    [SerializeField]
    private LevelAuthoringAction action = LevelAuthoringAction.Place;

    [SerializeField]
    private LevelAuthoringShape shape = LevelAuthoringShape.Single;

    [SerializeField]
    private VoxelType material = VoxelType.Dirt;

    [SerializeField]
    private WaterAmount waterAmount = WaterAmount.Full;

    [SerializeField]
    private PuzzleSide facing = PuzzleSide.North;

    [Min(1)]
    [SerializeField]
    private int maximumVoxelsPerOperation = 32768;

    [Header("Voxel Prefab Capture")]
    [SerializeField]
    private LevelPrefabDefinition prefabCaptureTarget;

    [SerializeField]
    private Vector3Int prefabCaptureMinimum;

    [SerializeField]
    private Vector3Int prefabCaptureSize = Vector3Int.one;

    [Header("Voxel Prefab Placement")]
    [SerializeField]
    private bool prefabPlacementEnabled;

    [SerializeField]
    private LevelPrefabDefinition prefabPlacementSource;

    [SerializeField]
    private LevelPrefabPlacementMode prefabPlacementMode =
        LevelPrefabPlacementMode.Additive;

    [SerializeField]
    private LevelPrefabRotation prefabPlacementRotation;

    private bool rebuildingAuthoringEntities;

    public LevelDefinition Definition => levelDefinition;
    public VoxelWorld World => voxelWorld;
    public bool VoxelToolEnabled =>
        voxelToolEnabled && !prefabPlacementEnabled;
    public LevelAuthoringAction Action => action;
    public LevelAuthoringShape Shape => shape;
    public VoxelType Material => material;
    public WaterAmount SelectedWaterAmount => waterAmount;
    public PuzzleSide Facing => facing;
    public bool IsRebuildingAuthoringEntities =>
        rebuildingAuthoringEntities;
    public LevelPrefabDefinition PrefabCaptureTarget =>
        prefabCaptureTarget;
    public Vector3Int PrefabCaptureMinimum =>
        prefabCaptureMinimum;
    public Vector3Int PrefabCaptureSize =>
        ClampPositiveSize(prefabCaptureSize);
    public bool PrefabPlacementEnabled => prefabPlacementEnabled;
    public LevelPrefabDefinition PrefabPlacementSource =>
        prefabPlacementSource;
    public LevelPrefabPlacementMode PrefabPlacementMode =>
        prefabPlacementMode;
    public LevelPrefabRotation PrefabPlacementRotation =>
        prefabPlacementRotation;

    public int MaximumVoxelsPerOperation =>
        Mathf.Max(1, maximumVoxelsPerOperation);

    public bool RebuildPreview()
    {
        if (Application.isPlaying ||
            levelDefinition == null ||
            voxelWorld == null)
        {
            return false;
        }

        MigrateLegacyScenePortals();
        voxelWorld.SetStartingLevel(levelDefinition);
        voxelWorld.LoadLevelDefinition(levelDefinition);
        RebuildAuthoringEntities();
        return true;
    }

    public bool ClearPreview()
    {
        if (Application.isPlaying || voxelWorld == null)
        {
            return false;
        }

        voxelWorld.ClearWorld();
        ClearAuthoringEntities();
        return true;
    }

    public bool RebuildAuthoringEntities()
    {
        if (Application.isPlaying ||
            levelDefinition == null ||
            voxelWorld == null)
        {
            return false;
        }

        rebuildingAuthoringEntities = true;

        try
        {
            Transform entityRoot =
                GetOrCreateAuthoringEntitiesRoot();

            for (int index = entityRoot.childCount - 1;
                 index >= 0;
                 index--)
            {
                DestroyImmediate(
                    entityRoot.GetChild(index).gameObject);
            }

            foreach (LevelEntityRecord record in
                     levelDefinition.Entities)
            {
                WaterPortal portal =
                    LevelEntityFactory.CreateWaterPortal(
                        record,
                        voxelWorld,
                        entityRoot,
                        runtimeCopy: false);

                portal?.ConfigureIdentity(
                    record.EntityId,
                    levelDefinition);
            }
        }
        finally
        {
            rebuildingAuthoringEntities = false;
        }

        return true;
    }

    public bool CaptureAuthoringEntities()
    {
        if (Application.isPlaying ||
            levelDefinition == null)
        {
            return false;
        }

        Transform entityRoot = FindAuthoringEntitiesRoot();
        List<LevelEntityRecord> records = new();

        if (entityRoot != null)
        {
            WaterPortal[] portals =
                entityRoot.GetComponentsInChildren<WaterPortal>(
                    includeInactive: true);

            foreach (WaterPortal portal in portals)
            {
                if (portal == null ||
                    (portal.AuthoringDefinition != null &&
                     portal.AuthoringDefinition != levelDefinition))
                {
                    continue;
                }

                portal.ConfigureIdentity(
                    portal.EntityId,
                    levelDefinition);

                LevelEntityRecord record =
                    portal.CreateEntityRecord();

                if (record != null)
                {
                    records.Add(record);
                }
            }
        }

        levelDefinition.ReplaceEntities(records);
        return true;
    }

    public bool SynchronizeAuthoringPortal(WaterPortal portal)
    {
        if (Application.isPlaying ||
            rebuildingAuthoringEntities ||
            levelDefinition == null ||
            portal == null ||
            (portal.AuthoringDefinition != null &&
             portal.AuthoringDefinition != levelDefinition))
        {
            return false;
        }

        portal.ConfigureIdentity(
            portal.EntityId,
            levelDefinition);

        LevelEntityRecord record =
            portal.CreateEntityRecord();

        if (record == null)
        {
            return false;
        }

        levelDefinition.UpsertEntity(record);
        return true;
    }

    public bool RemoveAuthoringEntity(string entityId)
    {
        return
            !Application.isPlaying &&
            !rebuildingAuthoringEntities &&
            levelDefinition != null &&
            levelDefinition.RemoveEntity(entityId);
    }

    public int CaptureVoxelPrefab(out int capturedEntityCount)
    {
        capturedEntityCount = 0;
        if (Application.isPlaying ||
            levelDefinition == null ||
            prefabCaptureTarget == null)
        {
            return -1;
        }

        Vector3Int captureSize =
            ClampPositiveSize(prefabCaptureSize);

        Vector3Int maximumInclusive =
            prefabCaptureMinimum +
            captureSize -
            Vector3Int.one;

        if (!levelDefinition.ContainsWorldPosition(
                prefabCaptureMinimum) ||
            !levelDefinition.ContainsWorldPosition(
                maximumInclusive))
        {
            return -1;
        }

        long capturedCellCount =
            (long)captureSize.x *
            captureSize.y *
            captureSize.z;

        if (capturedCellCount > MaximumVoxelsPerOperation)
        {
            return -1;
        }

        List<LevelVoxelRecord> localVoxels = new();

        foreach (LevelVoxelRecord record in
                 levelDefinition.Voxels)
        {
            Vector3Int position = record.Position;

            if (position.x < prefabCaptureMinimum.x ||
                position.y < prefabCaptureMinimum.y ||
                position.z < prefabCaptureMinimum.z ||
                position.x > maximumInclusive.x ||
                position.y > maximumInclusive.y ||
                position.z > maximumInclusive.z)
            {
                continue;
            }

            localVoxels.Add(
                new LevelVoxelRecord(
                    position - prefabCaptureMinimum,
                    record.Type,
                    record.Facing,
                    record.Amount));
        }

        List<LevelEntityRecord> localEntities = new();

        foreach (LevelEntityRecord record in
                 levelDefinition.Entities)
        {
            if (record != null &&
                record.IsFullyInside(
                    prefabCaptureMinimum,
                    maximumInclusive))
            {
                localEntities.Add(
                    record.CreatePrefabLocal(
                        prefabCaptureMinimum));
            }
        }

        prefabCaptureTarget.ReplaceContent(
            captureSize,
            localVoxels,
            localEntities);

        capturedEntityCount = localEntities.Count;
        return localVoxels.Count;
    }

    public Vector3Int GetRotatedPrefabSize()
    {
        if (levelDefinition == null ||
            prefabPlacementSource == null)
        {
            return Vector3Int.one;
        }

        Vector3Int sourceSize = prefabPlacementSource.Size;
        int turns = (int)prefabPlacementRotation;

        return turns % 2 == 0
            ? sourceSize
            : new Vector3Int(
                sourceSize.z,
                sourceSize.y,
                sourceSize.x);
    }

    public void RotatePrefabPlacement()
    {
        prefabPlacementRotation =
            (LevelPrefabRotation)(
                ((int)prefabPlacementRotation + 1) % 4);
    }

    public bool TryBuildPrefabPlacement(
        Vector3Int origin,
        out List<LevelVoxelState> states,
        out Vector3Int rotatedSize)
    {
        states = new List<LevelVoxelState>();
        rotatedSize = GetRotatedPrefabSize();

        if (Application.isPlaying ||
            levelDefinition == null ||
            prefabPlacementSource == null)
        {
            return false;
        }

        long volume =
            (long)rotatedSize.x *
            rotatedSize.y *
            rotatedSize.z;

        Vector3Int maximumInclusive =
            origin + rotatedSize - Vector3Int.one;

        if (volume > MaximumVoxelsPerOperation ||
            !levelDefinition.ContainsWorldPosition(origin) ||
            !levelDefinition.ContainsWorldPosition(maximumInclusive))
        {
            return false;
        }

        Dictionary<Vector3Int, LevelVoxelRecord> occupied = new();

        foreach (LevelVoxelRecord sourceRecord in
                 prefabPlacementSource.Voxels)
        {
            Vector3Int localPosition = RotateLocalPosition(
                sourceRecord.Position,
                prefabPlacementSource.Size,
                (int)prefabPlacementRotation);

            Vector3Int worldPosition = origin + localPosition;

            occupied[worldPosition] = new LevelVoxelRecord(
                worldPosition,
                sourceRecord.Type,
                RotateFacing(
                    sourceRecord.Facing,
                    (int)prefabPlacementRotation),
                sourceRecord.Amount);
        }

        if (prefabPlacementMode ==
            LevelPrefabPlacementMode.Additive)
        {
            states.Capacity = occupied.Count;

            foreach (LevelVoxelRecord record in occupied.Values)
            {
                states.Add(LevelVoxelState.Occupied(record));
            }

            return
                states.Count > 0 ||
                prefabPlacementSource.Entities.Count > 0;
        }

        states.Capacity = (int)volume;

        for (int x = 0; x < rotatedSize.x; x++)
        {
            for (int y = 0; y < rotatedSize.y; y++)
            {
                for (int z = 0; z < rotatedSize.z; z++)
                {
                    Vector3Int worldPosition =
                        origin + new Vector3Int(x, y, z);

                    states.Add(
                        occupied.TryGetValue(
                            worldPosition,
                            out LevelVoxelRecord record)
                            ? LevelVoxelState.Occupied(record)
                            : LevelVoxelState.Empty(worldPosition));
                }
            }
        }

        return true;
    }

    public bool TryBuildPrefabEntities(
        Vector3Int origin,
        out List<LevelEntityRecord> entities,
        bool createUniqueIds = false)
    {
        entities = new List<LevelEntityRecord>();

        if (prefabPlacementSource == null)
        {
            return false;
        }

        Vector3Int rotatedSize = GetRotatedPrefabSize();
        Vector3Int maximumInclusive =
            origin + rotatedSize - Vector3Int.one;

        if (!levelDefinition.ContainsWorldPosition(origin) ||
            !levelDefinition.ContainsWorldPosition(maximumInclusive))
        {
            return false;
        }

        foreach (LevelEntityRecord template in
                 prefabPlacementSource.Entities)
        {
            if (template != null)
            {
                entities.Add(
                    template.CreatePlacedCopy(
                        origin,
                        prefabPlacementSource.Size,
                        (int)prefabPlacementRotation,
                        createUniqueIds));
            }
        }

        return true;
    }

    public int ApplyVoxelPrefab(
        Vector3Int origin,
        out int placedEntityCount)
    {
        placedEntityCount = 0;

        if (!TryBuildPrefabPlacement(
                origin,
                out List<LevelVoxelState> targetStates,
                out _) ||
            !TryBuildPrefabEntities(
                origin,
                out List<LevelEntityRecord> placedEntities,
                createUniqueIds: true))
        {
            return -1;
        }

        List<Vector3Int> positions =
            new(targetStates.Count);

        foreach (LevelVoxelState state in targetStates)
        {
            positions.Add(state.Position);
        }

        List<LevelVoxelState> before =
            levelDefinition.CaptureStates(positions);

        Dictionary<Vector3Int, LevelVoxelState> beforeByPosition =
            new(before.Count);

        foreach (LevelVoxelState state in before)
        {
            beforeByPosition[state.Position] = state;
        }

        int changed = 0;

        foreach (LevelVoxelState targetState in targetStates)
        {
            if (!beforeByPosition.TryGetValue(
                    targetState.Position,
                    out LevelVoxelState beforeState) ||
                !StatesMatch(beforeState, targetState))
            {
                changed++;
            }
        }

        if (changed > 0)
        {
            levelDefinition.RestoreStates(targetStates);

            if (voxelWorld.HasLoadedChunks)
            {
                voxelWorld.SetAuthoringVoxelStates(targetStates);
            }
            else
            {
                voxelWorld.LoadLevelDefinition(levelDefinition);
            }
        }

        foreach (LevelEntityRecord entity in placedEntities)
        {
            levelDefinition.UpsertEntity(entity);
        }

        placedEntityCount = placedEntities.Count;

        if (placedEntityCount > 0)
        {
            RebuildAuthoringEntities();
        }

        return changed;
    }

    private static Vector3Int RotateLocalPosition(
        Vector3Int position,
        Vector3Int sourceSize,
        int quarterTurns)
    {
        return quarterTurns switch
        {
            1 => new Vector3Int(
                position.z,
                position.y,
                sourceSize.x - 1 - position.x),
            2 => new Vector3Int(
                sourceSize.x - 1 - position.x,
                position.y,
                sourceSize.z - 1 - position.z),
            3 => new Vector3Int(
                sourceSize.z - 1 - position.z,
                position.y,
                position.x),
            _ => position
        };
    }

    private static PuzzleSide RotateFacing(
        PuzzleSide facing,
        int quarterTurns)
    {
        string[] horizontal =
        {
            "North",
            "East",
            "South",
            "West"
        };

        int current = System.Array.IndexOf(
            horizontal,
            facing.ToString());

        if (current < 0)
        {
            return facing;
        }

        string rotatedName =
            horizontal[(current + quarterTurns) % horizontal.Length];

        return System.Enum.TryParse(
            rotatedName,
            out PuzzleSide rotated)
            ? rotated
            : facing;
    }

    private static bool StatesMatch(
        LevelVoxelState left,
        LevelVoxelState right)
    {
        if (left.HasVoxel != right.HasVoxel)
        {
            return false;
        }

        if (!left.HasVoxel)
        {
            return true;
        }

        return
            left.Record.Type == right.Record.Type &&
            left.Record.Facing == right.Record.Facing &&
            left.Record.Amount == right.Record.Amount;
    }

    public List<LevelVoxelState> CaptureVoxelStates(
        IReadOnlyCollection<Vector3Int> positions)
    {
        return levelDefinition != null
            ? levelDefinition.CaptureStates(positions)
            : new List<LevelVoxelState>();
    }

    public void RestoreVoxelStates(
        IReadOnlyCollection<LevelVoxelState> states)
    {
        if (Application.isPlaying ||
            levelDefinition == null ||
            voxelWorld == null ||
            states == null)
        {
            return;
        }

        levelDefinition.RestoreStates(states);

        if (voxelWorld.HasLoadedChunks)
        {
            voxelWorld.SetAuthoringVoxelStates(states);
        }
        else
        {
            voxelWorld.LoadLevelDefinition(levelDefinition);
        }
    }

    public int ApplyVoxelEdit(IReadOnlyCollection<Vector3Int> positions)
    {
        if (Application.isPlaying ||
            levelDefinition == null ||
            voxelWorld == null ||
            positions == null)
        {
            return 0;
        }

        VoxelType targetType =
            action == LevelAuthoringAction.Place
                ? material
                : VoxelType.Air;

        int changed = levelDefinition.SetVoxels(
            positions,
            targetType,
            facing,
            waterAmount);

        if (changed > 0)
        {
            if (voxelWorld.HasLoadedChunks)
            {
                voxelWorld.SetAuthoringVoxels(
                    positions,
                    targetType,
                    facing,
                    waterAmount);
            }
            else
            {
                voxelWorld.LoadLevelDefinition(levelDefinition);
            }
        }

        return changed;
    }

    public Transform FindAuthoringEntitiesRoot()
    {
        return transform.Find("Authoring Entities");
    }

    public Transform GetOrCreateAuthoringEntitiesRoot()
    {
        Transform existing = FindAuthoringEntitiesRoot();

        if (existing != null)
        {
            return existing;
        }

        GameObject parent = new("Authoring Entities");
        parent.transform.SetParent(transform, false);
        return parent.transform;
    }

    public bool CaptureRuntimeWorld()
    {
        if (!Application.isPlaying ||
            levelDefinition == null ||
            voxelWorld == null)
        {
            return false;
        }

        return voxelWorld.CaptureCurrentWorld(levelDefinition);
    }

    private void MigrateLegacyScenePortals()
    {
        if (levelDefinition == null ||
            levelDefinition.Entities.Count > 0)
        {
            return;
        }

        Transform entityRoot = FindAuthoringEntitiesRoot();

        if (entityRoot == null)
        {
            return;
        }

        WaterPortal[] portals =
            entityRoot.GetComponentsInChildren<WaterPortal>(
                includeInactive: true);

        bool hasUnownedPortal = false;

        foreach (WaterPortal portal in portals)
        {
            if (portal != null &&
                portal.AuthoringDefinition == null)
            {
                hasUnownedPortal = true;
                break;
            }
        }

        if (hasUnownedPortal)
        {
            CaptureAuthoringEntities();
        }
    }

    private void ClearAuthoringEntities()
    {
        Transform entityRoot = FindAuthoringEntitiesRoot();

        if (entityRoot == null)
        {
            return;
        }

        rebuildingAuthoringEntities = true;

        try
        {
            for (int index = entityRoot.childCount - 1;
                 index >= 0;
                 index--)
            {
                DestroyImmediate(
                    entityRoot.GetChild(index).gameObject);
            }
        }
        finally
        {
            rebuildingAuthoringEntities = false;
        }
    }

    private void Awake()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (voxelWorld != null)
        {
            voxelWorld.SetStartingLevel(levelDefinition);
        }

        Transform entityRoot = FindAuthoringEntitiesRoot();

        if (entityRoot != null)
        {
            entityRoot.gameObject.SetActive(false);
        }
    }

    private void OnValidate()
    {
        maximumVoxelsPerOperation =
            Mathf.Max(1, maximumVoxelsPerOperation);

        prefabCaptureSize =
            ClampPositiveSize(prefabCaptureSize);
    }

    private void OnDrawGizmosSelected()
    {
        if (levelDefinition == null)
        {
            return;
        }

        Vector3 minimum =
            (Vector3)(levelDefinition.OriginInChunks * Chunk.ChunkSize);

        Vector3 size =
            (Vector3)(levelDefinition.SizeInChunks * Chunk.ChunkSize);

        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.8f);
        Gizmos.DrawWireCube(minimum + size * 0.5f, size);

        Vector3 captureSize =
            PrefabCaptureSize;

        Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(
            (Vector3)prefabCaptureMinimum +
            captureSize * 0.5f,
            captureSize);
    }

    private static Vector3Int ClampPositiveSize(
        Vector3Int value)
    {
        return new Vector3Int(
            Mathf.Max(1, value.x),
            Mathf.Max(1, value.y),
            Mathf.Max(1, value.z));
    }
}
