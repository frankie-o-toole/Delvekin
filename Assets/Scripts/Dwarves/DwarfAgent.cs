using UnityEngine;

public class DwarfAgent : MonoBehaviour
{
    public bool IsActive { get; private set; }

    public Vector3Int CurrentVoxel { get; private set; }
    public Vector3Int TargetVoxel { get; private set; }

    public PuzzleSide Facing { get; private set; }
    
    private Renderer[] renderers;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        DwarfVisibilitySystem.Register(this);
    }
    public void SetVisibility(bool visible)
    {
        foreach (Renderer renderer in renderers)
        {
            renderer.enabled = visible;
        }
    }
    public void SetFacing(PuzzleSide facing)
    {
        Facing = facing;
    }

    public void Activate(Vector3Int spawnVoxel)
    {
        IsActive = true;

        CurrentVoxel = spawnVoxel;
        Facing = PuzzleSide.North;

        transform.position = VoxelMath.VoxelCenter(spawnVoxel);

        DwarfVisibilitySystem.RefreshDwarf(this);
    }

    public void Deactivate()
    {
        IsActive = false;
        gameObject.SetActive(false);
    }

    public void Tick()
    {
        if (!IsActive) return;

        // movement/AI later
    }

    public void SetCurrentVoxel(Vector3Int voxel)
    {
        CurrentVoxel = voxel;
        transform.position = VoxelToWorld(voxel);

        DwarfVisibilitySystem.RefreshDwarf(this);
    }

    public void SetTargetVoxel(Vector3Int voxel)
    {
        TargetVoxel = voxel;
    }

    private Vector3 VoxelToWorld(Vector3Int v)
    {
        return new Vector3(v.x + 0.5f, v.y + 0.5f, v.z + 0.5f);
    }
}