using UnityEngine;

[AddComponentMenu("Delvekin/Water Outlet Portal")]
public sealed class WaterOutletPortal : WaterPortal
{
    [Header("Outlet")]
    [SerializeField]
    [Tooltip("Zero derives capacity from the portal volume.")]
    private int capacityOverride;

    private bool registered;
    private int registeredStateHash = int.MinValue;

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

    private void Update()
    {
        if (!registered)
        {
            Register();
            return;
        }

        int currentStateHash = CalculateStateHash();

        if (currentStateHash == registeredStateHash)
        {
            return;
        }

        registeredStateHash = currentStateHash;
        voxelWorld.NotifyWaterOutletPortalChanged(this);
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

        registered =
            voxelWorld.RegisterWaterOutletPortal(this);

        if (registered)
        {
            registeredStateHash = CalculateStateHash();
        }
    }

    private int CalculateStateHash()
    {
        unchecked
        {
            int hash = MinimumVoxel.GetHashCode();
            hash = hash * 31 + Size.GetHashCode();
            hash = hash * 31 + Facing.GetHashCode();
            hash = hash * 31 + capacityOverride;
            return hash;
        }
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
