using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class FluidChunkRenderer : MonoBehaviour
{
    private readonly List<Color> colors = new();
    private readonly List<Vector3> vertices = new();
    private readonly List<int> triangles = new();

    private Mesh mesh;
    private Chunk chunk;
    private VoxelWorld world;

    public void Initialize(Chunk chunk, VoxelWorld world)
    {
        this.chunk = chunk;
        this.world = world;

        mesh = new Mesh
        {
            name = $"Fluid Mesh {chunk.ChunkCoordinate}"
        };

        GetComponent<MeshFilter>().mesh = mesh;
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
                    Voxel voxel = chunk.GetVoxel(x, y, z);

                    if (!IsFluid(voxel.Type))
                    {
                        continue;
                    }

                    Vector3Int worldPosition =
                        chunk.ChunkCoordinate * Chunk.ChunkSize +
                        new Vector3Int(x, y, z);

                    if (!VoxelVisibilitySystem.IsVoxelVisible(worldPosition))
                    {
                        continue;
                    }

                    AddFluidFaces(
                        new Vector3Int(x, y, z),
                        worldPosition,
                        voxel.Type);
                }
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void AddFluidFaces(
        Vector3Int localPosition,
        Vector3Int worldPosition,
        VoxelType type)
    {
        Vector3 p = localPosition;
        float height = Mathf.Max(
            0.01f,
            world.GetFluidFill01(worldPosition));

        Color color = type == VoxelType.Water
            ? new Color(0.05f, 0.55f, 0.7f)
            : Color.red;

        Vector3Int above = worldPosition + Vector3Int.up;

        if (height < 0.999f || IsFluidFaceExposed(above, type))
        {
            AddQuad(
                p + new Vector3(0, height, 0),
                p + new Vector3(1, height, 0),
                p + new Vector3(1, height, 1),
                p + new Vector3(0, height, 1),
                Shade(color, 1.16f));
        }

        if (IsFluidFaceExposed(worldPosition + Vector3Int.down, type))
        {
            AddQuad(
                p + new Vector3(0, 0, 0),
                p + new Vector3(0, 0, 1),
                p + new Vector3(1, 0, 1),
                p + new Vector3(1, 0, 0),
                Shade(color, 0.55f));
        }

        AddSide(
            worldPosition + Vector3Int.forward,
            type,
            height,
            p + new Vector3(0, 0, 1),
            p + new Vector3(0, height, 1),
            p + new Vector3(1, height, 1),
            p + new Vector3(1, 0, 1),
            Shade(color, 0.82f));

        AddSide(
            worldPosition + Vector3Int.back,
            type,
            height,
            p + new Vector3(1, 0, 0),
            p + new Vector3(1, height, 0),
            p + new Vector3(0, height, 0),
            p + new Vector3(0, 0, 0),
            Shade(color, 0.94f));

        AddSide(
            worldPosition + Vector3Int.right,
            type,
            height,
            p + new Vector3(1, 0, 1),
            p + new Vector3(1, height, 1),
            p + new Vector3(1, height, 0),
            p + new Vector3(1, 0, 0),
            Shade(color, 0.72f));

        AddSide(
            worldPosition + Vector3Int.left,
            type,
            height,
            p + new Vector3(0, 0, 0),
            p + new Vector3(0, height, 0),
            p + new Vector3(0, height, 1),
            p + new Vector3(0, 0, 1),
            Shade(color, 0.87f));
    }

    private void AddSide(
        Vector3Int neighbourPosition,
        VoxelType type,
        float height,
        Vector3 bottomA,
        Vector3 topA,
        Vector3 topB,
        Vector3 bottomB,
        Color color)
    {
        if (!TryGetSideBottom(
                neighbourPosition,
                type,
                height,
                out float neighbourHeight))
        {
            return;
        }

        bottomA.y += neighbourHeight;
        bottomB.y += neighbourHeight;

        AddQuad(bottomA, topA, topB, bottomB, color);
    }

    private bool TryGetSideBottom(
        Vector3Int neighbourPosition,
        VoxelType type,
        float height,
        out float neighbourHeight)
    {
        neighbourHeight = 0f;

        if (!VoxelVisibilitySystem.IsVoxelVisible(neighbourPosition))
        {
            return true;
        }

        Voxel neighbour = world.GetVoxel(neighbourPosition);

        if (!IsFluid(neighbour.Type))
        {
            return neighbour.Type == VoxelType.Air;
        }

        if (neighbour.Type != type)
        {
            return true;
        }

        neighbourHeight = world.GetFluidFill01(neighbourPosition);
        return neighbourHeight + 0.001f < height;
    }

    private bool IsFluidFaceExposed(
        Vector3Int neighbourPosition,
        VoxelType type)
    {
        if (!VoxelVisibilitySystem.IsVoxelVisible(neighbourPosition))
        {
            return true;
        }

        VoxelType neighbourType = world.GetVoxel(neighbourPosition).Type;

        return neighbourType == VoxelType.Air ||
               (IsFluid(neighbourType) && neighbourType != type);
    }

    private void AddQuad(
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

        triangles.Add(index);
        triangles.Add(index + 2);
        triangles.Add(index + 1);
        triangles.Add(index);
        triangles.Add(index + 3);
        triangles.Add(index + 2);
    }

    private static Color Shade(Color color, float brightness)
    {
        return new Color(
            color.r * brightness,
            color.g * brightness,
            color.b * brightness,
            color.a);
    }

    private static bool IsFluid(VoxelType type)
    {
        return type == VoxelType.Water ||
               type == VoxelType.Lava;
    }
}
