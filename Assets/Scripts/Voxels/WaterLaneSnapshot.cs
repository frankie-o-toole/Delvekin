using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Immutable gameplay view of one source-fed water body's transport lanes.
/// A completed rebuild publishes a new snapshot; drifting dwarfs may keep
/// reading the previous instance while water redistribution is in progress.
/// </summary>
public sealed class WaterLaneSnapshot
{
    private readonly Dictionary<Vector3Int, WaterLaneSample> samples;

    public int BodyId { get; }
    public int Version { get; }
    public int SampleCount => samples.Count;

    public WaterLaneSnapshot(
        int bodyId,
        int version,
        Dictionary<Vector3Int, WaterLaneSample> samples)
    {
        BodyId = bodyId;
        Version = version;
        this.samples = samples ??
            new Dictionary<Vector3Int, WaterLaneSample>();
    }

    public bool TryGetSample(
        Vector3Int surfaceVoxel,
        out WaterLaneSample sample)
    {
        return samples.TryGetValue(surfaceVoxel, out sample);
    }
}

/// <summary>
/// Cached guidance at one water-surface voxel. Centre is the middle of the
/// local river cross-section; Tangent is downstream and may be diagonal.
/// </summary>
public readonly struct WaterLaneSample
{
    public Vector3 SurfacePosition { get; }
    public Vector3 Centre { get; }
    public Vector3 Tangent { get; }
    public float Width { get; }

    public WaterLaneSample(
        Vector3 surfacePosition,
        Vector3 centre,
        Vector3 tangent,
        float width)
    {
        SurfacePosition = surfacePosition;
        Centre = centre;
        Tangent = tangent.sqrMagnitude > 0f
            ? tangent.normalized
            : Vector3.zero;
        Width = Mathf.Max(1f, width);
    }
}
