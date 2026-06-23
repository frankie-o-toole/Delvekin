using UnityEngine;

public struct CameraSnapShot
{
    public Vector3 Position;
    public Quaternion Rotation;

    public CameraSnapShot(
        Vector3 position, 
        Quaternion rotation)
    {
        Position = position;
        Rotation = rotation;
    }
}
