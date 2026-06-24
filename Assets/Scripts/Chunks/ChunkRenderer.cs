using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ChunkRenderer : MonoBehaviour
{
    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    private Chunk chunk;

    public void Initialize(Chunk chunk)
    {
        this.chunk = chunk;

        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
        
        mesh = new Mesh();
        meshFilter.mesh = mesh;

        if (meshCollider == null)
        meshCollider = gameObject.AddComponent<MeshCollider>();  
        
        RebuildMesh();
    }

    public void RebuildMesh()
    {
        mesh.Clear();

        List<Color> colors = new();
        List<Vector3> vertices = new();
        List<int> triangles = new();

        for (int x = 0; x < Chunk.ChunkSize; x++)
            for (int y = 0; y < Chunk.ChunkSize; y++)
                for (int z = 0; z < Chunk.ChunkSize; z++)
                {
                    Voxel voxel = chunk.GetVoxel(x, y, z);

                    if (!voxel.IsSolid())
                        continue;

                    // World Position 
                    Vector3Int worldPos = chunk.ChunkCoordinate * Chunk.ChunkSize
                                         + new Vector3Int(x, y, z);

                    // Visibility Check
                    if (!VoxelVisibilitySystem.IsVoxelVisible(worldPos))
                        continue;

                    AddCubeFaces(x, y, z, voxel, vertices, triangles, colors);
                }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.colors = colors.ToArray();

        mesh.RecalculateNormals();

        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
    }

    private void AddCubeFaces(
     int x, int y, int z,
     Voxel voxel,
     List<Vector3> vertices,
     List<int> triangles,
     List<Color> colors)
    {
        Color color = GetVoxelColor(voxel.Type);
        Vector3 p = new Vector3(x, y, z);

        // TOP
        if (IsAir(x, y + 1, z))
            AddQuad(vertices, triangles, colors,
                p + new Vector3(0, 1, 0),
                p + new Vector3(1, 1, 0),
                p + new Vector3(1, 1, 1),
                p + new Vector3(0, 1, 1),
                color);

        // BOTTOM
        if (IsAir(x, y - 1, z))
            AddQuad(vertices, triangles, colors,
                p + new Vector3(0, 0, 0),
                p + new Vector3(0, 0, 1),
                p + new Vector3(1, 0, 1),
                p + new Vector3(1, 0, 0),
                color);

        // NORTH (forward)
        if (IsAir(x, y, z + 1))
            AddQuad(vertices, triangles, colors,
                p + new Vector3(0, 0, 1),
                p + new Vector3(0, 1, 1),
                p + new Vector3(1, 1, 1),
                p + new Vector3(1, 0, 1),
                color);

        // SOUTH (back)
        if (IsAir(x, y, z - 1))
            AddQuad(vertices, triangles, colors,
                p + new Vector3(1, 0, 0),
                p + new Vector3(1, 1, 0),
                p + new Vector3(0, 1, 0),
                p + new Vector3(0, 0, 0),
                color);

        // EAST (right)
        if (IsAir(x + 1, y, z))
            AddQuad(vertices, triangles, colors,
                p + new Vector3(1, 0, 1),
                p + new Vector3(1, 1, 1),
                p + new Vector3(1, 1, 0),
                p + new Vector3(1, 0, 0),
                color);

        // WEST (left)
        if (IsAir(x - 1, y, z))
            AddQuad(vertices, triangles, colors,
                p + new Vector3(0, 0, 0),
                p + new Vector3(0, 1, 0),
                p + new Vector3(0, 1, 1),
                p + new Vector3(0, 0, 1),
                color);
    }

    private bool IsAir(int x, int y, int z)
    {
        if (x < 0 || x >= Chunk.ChunkSize ||
            y < 0 || y >= Chunk.ChunkSize ||
            z < 0 || z >= Chunk.ChunkSize)
            return true; // treat outside as air

        return chunk.GetVoxel(x, y, z).Type == VoxelType.Air;
    }

    private void AddQuad(
    List<Vector3> vertices,
    List<int> triangles,
    List<Color> colors,
    Vector3 a,
    Vector3 b,
    Vector3 c,
    Vector3 d,
    Color color)
    {
        int index = vertices.Count;

        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);

        colors.Add(color);
        colors.Add(color);
        colors.Add(color);
        colors.Add(color);

        // consistent clockwise winding (IMPORTANT)
        triangles.Add(index + 0);
        triangles.Add(index + 2);
        triangles.Add(index + 1);

        triangles.Add(index + 0);
        triangles.Add(index + 3);
        triangles.Add(index + 2);
    }
    private Color GetVoxelColor(VoxelType type)
    {
        return type switch
        {
            VoxelType.Dirt => new Color(0.56f, 0.26f, 0.13f),
            VoxelType.Granite => new Color(0.5f, 0.5f, 0.55f),
            VoxelType.Lava => Color.red,
            VoxelType.Water => new Color(0.05f, 0.55f, 0.7f),
            VoxelType.Vine => new Color(0, 0.25f, 0),
            VoxelType.Snow => Color.white,
            VoxelType.Bubblegum => Color.pink,

            _ => Color.magenta
        };
    }

}