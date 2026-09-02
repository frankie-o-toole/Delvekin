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

    // Mesh rebuilds happen frequently while scrolling Puzzle slices. Reusing
    // these buffers avoids allocating several arrays and lists per chunk.
    private readonly List<Color> colors = new();
    private readonly List<Vector3> vertices = new();
    private readonly List<int> triangles = new();

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

        colors.Clear();
        vertices.Clear();
        triangles.Clear();

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

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetColors(colors);

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
        Vector3Int worldPos =
            chunk.ChunkCoordinate * Chunk.ChunkSize +
            new Vector3Int(x, y, z);

        Color color =
            GetVoxelColor(
                voxel.Type,
                worldPos);

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
                ShadeColor(color, 1.16f));
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
                ShadeColor(color, 0.55f));
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
                ShadeColor(color, 0.82f));
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
                ShadeColor(color, 0.94f));
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
                ShadeColor(color, 0.72f));
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
                ShadeColor(color, 0.87f));
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

    private static Color ShadeColor(
        Color color,
        float brightness)
    {
        return new Color(
            color.r * brightness,
            color.g * brightness,
            color.b * brightness,
            color.a);
    }

    private Color GetVoxelColor(
        VoxelType type,
        Vector3Int worldPosition)
    {
        Color baseColor = type switch
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

            VoxelType.Stair =>
                new Color(0.62f, 0.38f, 0.16f),

            VoxelType.Ladder =>
                new Color(0.78f, 0.58f, 0.22f),

            VoxelType.Bubblegum =>
                Color.pink,

            VoxelType.SpawnPoint =>
                Color.lightGreen,

            _ =>
                Color.magenta
        };

        if (type != VoxelType.Dirt &&
            type != VoxelType.Granite)
        {
            return baseColor;
        }

        // Broad patches break up large single-colour surfaces without adding
        // new voxel types or save data. The low frequency avoids visual noise.
        float patch = Mathf.Lerp(
            0.90f,
            1.06f,
            Mathf.PerlinNoise(
                worldPosition.x * 0.045f,
                worldPosition.z * 0.045f));

        // Four-voxel strata make height and geological structure easier to
        // read. The contrast is intentionally subtle enough to remain Dirt.
        int band =
            Mathf.FloorToInt(
                worldPosition.y / 4f);

        float stratum = (band % 3) switch
        {
            0 => 0.91f,
            1 => 1.00f,
            _ => 0.96f
        };

        return ShadeColor(
            baseColor,
            patch * stratum);
    }
}
