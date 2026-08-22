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
    private VoxelWorld voxelWorld;

    public void Initialize(
        Chunk chunk,
        VoxelWorld voxelWorld)
    {
        this.chunk = chunk;
        this.voxelWorld = voxelWorld;

        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();

        mesh = new Mesh();

        mesh.name = $"Chunk Mesh {chunk.ChunkCoordinate}";

        meshFilter.mesh = mesh;

        if (meshCollider == null)
        {
            meshCollider =
                gameObject.AddComponent<MeshCollider>();
        }

        RebuildMesh();
    }

    public void RebuildMesh()
    {
        mesh.Clear();

        List<Color> colors = new();
        List<Vector3> vertices = new();
        List<int> triangles = new();

        for (int x = 0; x < Chunk.ChunkSize; x++)
        {
            for (int y = 0; y < Chunk.ChunkSize; y++)
            {
                for (int z = 0; z < Chunk.ChunkSize; z++)
                {
                    Voxel voxel =
                        chunk.GetVoxel(x, y, z);

                    if (!voxel.IsSolid())
                        continue;

                    Vector3Int worldPos =
                        chunk.ChunkCoordinate * Chunk.ChunkSize +
                        new Vector3Int(x, y, z);

                    if (!VoxelVisibilitySystem.IsVoxelVisible(worldPos))
                        continue;

                    AddCubeFaces(
                        x,
                        y,
                        z,
                        voxel,
                        vertices,
                        triangles,
                        colors);
                }
            }
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.colors = colors.ToArray();

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshCollider.sharedMesh = null;

        if (mesh.vertexCount > 0)
        {
            meshCollider.sharedMesh = mesh;
        }
    }

    private void AddCubeFaces(
        int x,
        int y,
        int z,
        Voxel voxel,
        List<Vector3> vertices,
        List<int> triangles,
        List<Color> colors)
    {
        Color color = GetVoxelColor(voxel.Type);

        Vector3 p = new(x, y, z);

        // TOP
        if (IsFaceExposed(x, y + 1, z))
        {
            AddQuad(
                vertices,
                triangles,
                colors,
                p + new Vector3(0, 1, 0),
                p + new Vector3(1, 1, 0),
                p + new Vector3(1, 1, 1),
                p + new Vector3(0, 1, 1),
                color);
        }

        // BOTTOM
        if (IsFaceExposed(x, y - 1, z))
        {
            AddQuad(
                vertices,
                triangles,
                colors,
                p + new Vector3(0, 0, 0),
                p + new Vector3(0, 0, 1),
                p + new Vector3(1, 0, 1),
                p + new Vector3(1, 0, 0),
                color);
        }

        // NORTH
        if (IsFaceExposed(x, y, z + 1))
        {
            AddQuad(
                vertices,
                triangles,
                colors,
                p + new Vector3(0, 0, 1),
                p + new Vector3(0, 1, 1),
                p + new Vector3(1, 1, 1),
                p + new Vector3(1, 0, 1),
                color);
        }

        // SOUTH
        if (IsFaceExposed(x, y, z - 1))
        {
            AddQuad(
                vertices,
                triangles,
                colors,
                p + new Vector3(1, 0, 0),
                p + new Vector3(1, 1, 0),
                p + new Vector3(0, 1, 0),
                p + new Vector3(0, 0, 0),
                color);
        }

        // EAST
        if (IsFaceExposed(x + 1, y, z))
        {
            AddQuad(
                vertices,
                triangles,
                colors,
                p + new Vector3(1, 0, 1),
                p + new Vector3(1, 1, 1),
                p + new Vector3(1, 1, 0),
                p + new Vector3(1, 0, 0),
                color);
        }

        // WEST
        if (IsFaceExposed(x - 1, y, z))
        {
            AddQuad(
                vertices,
                triangles,
                colors,
                p + new Vector3(0, 0, 0),
                p + new Vector3(0, 1, 0),
                p + new Vector3(0, 1, 1),
                p + new Vector3(0, 0, 1),
                color);
        }
    }

    private bool IsFaceExposed(
        int neighborX,
        int neighborY,
        int neighborZ)
    {
        Vector3Int worldNeighborPos =
            chunk.ChunkCoordinate * Chunk.ChunkSize +
            new Vector3Int(
                neighborX,
                neighborY,
                neighborZ);

        Voxel neighborVoxel =
            voxelWorld.GetVoxel(worldNeighborPos);

        if (neighborVoxel.Type == VoxelType.Air)
            return true;

        if (!VoxelVisibilitySystem.IsVoxelVisible(worldNeighborPos))
            return true;

        return false;
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
            VoxelType.Dirt =>
                new Color(0.56f, 0.26f, 0.13f),

            VoxelType.Granite =>
                new Color(0.5f, 0.5f, 0.55f),

            VoxelType.Lava =>
                Color.red,

            VoxelType.Water =>
                new Color(0.05f, 0.55f, 0.7f),

            VoxelType.Vine =>
                new Color(0, 0.25f, 0),

            VoxelType.Snow =>
                Color.white,

            VoxelType.Bubblegum =>
                Color.pink,

            VoxelType.SpawnPoint =>
                Color.lightGreen,

            _ =>
                Color.magenta
        };
    }
}