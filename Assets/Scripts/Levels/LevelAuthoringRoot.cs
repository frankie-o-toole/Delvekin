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

    private bool rebuildingAuthoringEntities;

    public LevelDefinition Definition => levelDefinition;
    public VoxelWorld World => voxelWorld;
    public bool VoxelToolEnabled => voxelToolEnabled;
    public LevelAuthoringAction Action => action;
    public LevelAuthoringShape Shape => shape;
    public VoxelType Material => material;
    public WaterAmount SelectedWaterAmount => waterAmount;
    public PuzzleSide Facing => facing;
    public bool IsRebuildingAuthoringEntities =>
        rebuildingAuthoringEntities;

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
    }
}
