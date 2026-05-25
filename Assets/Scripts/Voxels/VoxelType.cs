using System.Collections;
using UnityEngine;

public enum VoxelType
{
    Air,
    Dirt,
    Granite
}

public class VoxelDebug : MonoBehaviour
{
    Voxel voxel = new Voxel(VoxelType.Dirt);

    // Update is called once per frame
    void Update()
    {
        Debug.Log(voxel.Type);
        Debug.Log(voxel.IsSolid());

    }
}

