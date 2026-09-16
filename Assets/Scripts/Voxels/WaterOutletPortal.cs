using UnityEngine;

[AddComponentMenu("Delvekin/Water Outlet Portal")]
public sealed class WaterOutletPortal : WaterPortal
{
    [Header("Outlet")]
    [SerializeField]
    [Tooltip("Zero derives capacity from the portal volume.")]
    private int capacityOverride;

    private bool registered;

    public int Capacity =>
        capacityOverride > 0
            ? capacityOverride
            : CoveredCellCount;

    protected override Color GizmoColor =>
        new(1f, 0.2f, 0.85f, 1f);

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

        voxelWorld.RegisterWaterOutletPortal(this);
        registered = true;
    }

    private void Unregister()
    {
        if (!registered || voxelWorld == null)
        {
            return;
        }

        voxelWorld.UnregisterWaterOutletPortal(this);
        registered = false;
    }
}
