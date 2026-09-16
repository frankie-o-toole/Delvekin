using UnityEngine;

[AddComponentMenu("Delvekin/Water Source Portal")]
public sealed class WaterSourcePortal : WaterPortal
{
    [Header("Source")]
    [SerializeField]
    private int maximumLevelOffset;

    [SerializeField]
    [Min(1)]
    private int supplyUnitsPerTick = 128;

    private bool registered;

    public int SupplyUnitsPerTick => Mathf.Max(1, supplyUnitsPerTick);

    public int MaximumLevelY =>
        MinimumVoxel.y + Size.y - 1 + maximumLevelOffset;

    protected override Color GizmoColor =>
        new(0.15f, 1f, 0.35f, 1f);

    private void Start()
    {
        Register();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            Register();
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            Unregister();
        }
    }

    private void Register()
    {
        if (registered)
        {
            return;
        }

        ResolveWorld();

        if (voxelWorld == null)
        {
            return;
        }

        voxelWorld.RegisterWaterSourcePortal(this);
        registered = true;
    }

    private void Unregister()
    {
        if (!registered || voxelWorld == null)
        {
            return;
        }

        voxelWorld.UnregisterWaterSourcePortal(this);
        registered = false;
    }
}
