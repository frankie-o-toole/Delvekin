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

    public LevelDefinition Definition => levelDefinition;
    public VoxelWorld World => voxelWorld;
    public bool VoxelToolEnabled => voxelToolEnabled;
    public LevelAuthoringAction Action => action;
    public LevelAuthoringShape Shape => shape;
    public VoxelType Material => material;
    public WaterAmount SelectedWaterAmount => waterAmount;
    public PuzzleSide Facing => facing;
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

        voxelWorld.LoadLevelDefinition(levelDefinition);
        return true;
    }

    public bool ClearPreview()
    {
        if (Application.isPlaying || voxelWorld == null)
        {
            return false;
        }

        voxelWorld.ClearWorld();
        return true;
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
                // Script reloads reset VoxelWorld's non-serialized chunk maps
                // even when old preview meshes are still visible.
                voxelWorld.LoadLevelDefinition(levelDefinition);
            }
        }

        return changed;
    }

    public Transform FindAuthoringEntitiesRoot()
    {
        return transform.Find("Authoring Entities");
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
