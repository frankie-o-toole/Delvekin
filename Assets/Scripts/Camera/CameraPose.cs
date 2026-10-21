using UnityEngine;

public readonly struct CameraPose
{
    public Vector3 Position { get; }
    public Quaternion Rotation { get; }

    public CameraPose(
        Vector3 position,
        Quaternion rotation)
    {
        Position = position;
        Rotation = rotation;
    }
}
