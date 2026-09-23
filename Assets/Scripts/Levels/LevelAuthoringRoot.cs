using UnityEngine;

public sealed class LevelAuthoringRoot : MonoBehaviour
{
    [SerializeField]
    private LevelDefinition levelDefinition;

    [Tooltip("VoxelWorld used to render the generated preview and runtime copy.")]
    [SerializeField]
    private VoxelWorld voxelWorld;

    public LevelDefinition Definition => levelDefinition;
    public VoxelWorld World => voxelWorld;

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
