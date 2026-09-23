using UnityEngine;

[AddComponentMenu("Delvekin/Water Source Portal")]
public sealed class WaterSourcePortal : WaterPortal
{
    [Header("Source")]
    [SerializeField]
    [Tooltip(
        "By default the source preserves the highest authored water surface " +
        "of the body it overlaps. Enable this to use an explicit world Y.")]
    private bool overrideMaximumFillY;

    [SerializeField]
    [Tooltip("Absolute world-space voxel Y used when the override is enabled.")]
    private int maximumFillY;

    [SerializeField]
    [Min(1)]
    private int supplyUnitsPerTick = 128;

    private bool registered;
    private int registeredStateHash = int.MinValue;

    public int SupplyUnitsPerTick => Mathf.Max(1, supplyUnitsPerTick);
    public bool OverrideMaximumFillY => overrideMaximumFillY;
    public int MaximumFillY => maximumFillY;

    protected override Color GizmoColor =>
        new(0.15f, 1f, 0.35f, 1f);

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
        voxelWorld.NotifyWaterSourcePortalChanged(this);
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
            voxelWorld.RegisterWaterSourcePortal(this);

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
            hash = hash * 31 + overrideMaximumFillY.GetHashCode();
            hash = hash * 31 + maximumFillY;
            hash = hash * 31 + supplyUnitsPerTick;
            return hash;
        }
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
