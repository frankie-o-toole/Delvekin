using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class FluidChunkRenderer : MonoBehaviour
{
    private static readonly ProfilerMarker RebuildMarker =
        new("Delvekin.Fluid.RebuildMesh");

    private readonly List<Color> colors = new();
    private readonly List<Vector3> vertices = new();
    private readonly List<int> triangles = new();

    private Mesh mesh;
    private Chunk chunk;
    private VoxelWorld world;

    private readonly struct SurfaceHeights
    {
        public readonly float SouthWest;
        public readonly float SouthEast;
        public readonly float NorthEast;
        public readonly float NorthWest;

        public SurfaceHeights(
            float southWest,
            float southEast,
            float northEast,
            float northWest)
        {
            SouthWest = southWest;
            SouthEast = southEast;
            NorthEast = northEast;
            NorthWest = northWest;
        }

        public static SurfaceHeights Flat(float height)
        {
            return new SurfaceHeights(height, height, height, height);
        }
    }

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
        using var marker = RebuildMarker.Auto();

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
        float fillHeight = Mathf.Max(
            0.01f,
            world.GetFluidFill01(worldPosition));

        Color color = type == VoxelType.Water
            ? new Color(0.05f, 0.55f, 0.7f)
            : Color.red;

        SurfaceHeights surface =
            GetSurfaceHeights(worldPosition, type, fillHeight);

        Vector3Int above = worldPosition + Vector3Int.up;

        if (fillHeight < 0.999f || IsFluidFaceExposed(above, type))
        {
            AddTopSurface(
                p,
                surface,
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
            fillHeight,
            p + new Vector3(0, 0, 1),
            p + new Vector3(0, surface.NorthWest, 1),
            p + new Vector3(1, surface.NorthEast, 1),
            p + new Vector3(1, 0, 1),
            Shade(color, 0.82f));

        AddSide(
            worldPosition + Vector3Int.back,
            type,
            fillHeight,
            p + new Vector3(1, 0, 0),
            p + new Vector3(1, surface.SouthEast, 0),
            p + new Vector3(0, surface.SouthWest, 0),
            p + new Vector3(0, 0, 0),
            Shade(color, 0.94f));

        AddSide(
            worldPosition + Vector3Int.right,
            type,
            fillHeight,
            p + new Vector3(1, 0, 1),
            p + new Vector3(1, surface.NorthEast, 1),
            p + new Vector3(1, surface.SouthEast, 0),
            p + new Vector3(1, 0, 0),
            Shade(color, 0.72f));

        AddSide(
            worldPosition + Vector3Int.left,
            type,
            fillHeight,
            p + new Vector3(0, 0, 0),
            p + new Vector3(0, surface.SouthWest, 0),
            p + new Vector3(0, surface.NorthWest, 1),
            p + new Vector3(0, 0, 1),
            Shade(color, 0.87f));
    }

    private SurfaceHeights GetSurfaceHeights(
        Vector3Int position,
        VoxelType type,
        float fillHeight)
    {
        if (type != VoxelType.Water ||
            fillHeight > 0.501f ||
            world.GetVoxel(position + Vector3Int.up).Type ==
                VoxelType.Water)
        {
            return SurfaceHeights.Flat(fillHeight);
        }

        bool openNorth =
            world.GetVoxel(position + Vector3Int.forward).Type !=
            VoxelType.Water;

        bool openSouth =
            world.GetVoxel(position + Vector3Int.back).Type !=
            VoxelType.Water;

        bool openEast =
            world.GetVoxel(position + Vector3Int.right).Type !=
            VoxelType.Water;

        bool openWest =
            world.GetVoxel(position + Vector3Int.left).Type !=
            VoxelType.Water;

        int openCount =
            (openNorth ? 1 : 0) +
            (openSouth ? 1 : 0) +
            (openEast ? 1 : 0) +
            (openWest ? 1 : 0);

        const float High = 1f;
        const float Low = 0f;

        if (openCount == 1)
        {
            if (openNorth)
                return new SurfaceHeights(High, High, Low, Low);

            if (openSouth)
                return new SurfaceHeights(Low, Low, High, High);

            if (openEast)
                return new SurfaceHeights(High, Low, Low, High);

            return new SurfaceHeights(Low, High, High, Low);
        }

        if (openCount == 2)
        {
            if (openNorth && openEast)
                return new SurfaceHeights(High, Low, Low, Low);

            if (openNorth && openWest)
                return new SurfaceHeights(Low, High, Low, Low);

            if (openSouth && openEast)
                return new SurfaceHeights(Low, Low, Low, High);

            if (openSouth && openWest)
                return new SurfaceHeights(Low, Low, High, Low);
        }

        // Isolated cells, straight channels and ambiguous configurations stay
        // visibly half full instead of collapsing into degenerate geometry.
        return SurfaceHeights.Flat(0.5f);
    }

    private void AddTopSurface(
        Vector3 origin,
        SurfaceHeights heights,
        Color color)
    {
        Vector3 southWest =
            origin + new Vector3(0, heights.SouthWest, 0);
        Vector3 southEast =
            origin + new Vector3(1, heights.SouthEast, 0);
        Vector3 northEast =
            origin + new Vector3(1, heights.NorthEast, 1);
        Vector3 northWest =
            origin + new Vector3(0, heights.NorthWest, 1);

        const float HighThreshold = 0.999f;
        int highCount =
            (heights.SouthWest >= HighThreshold ? 1 : 0) +
            (heights.SouthEast >= HighThreshold ? 1 : 0) +
            (heights.NorthEast >= HighThreshold ? 1 : 0) +
            (heights.NorthWest >= HighThreshold ? 1 : 0);

        // A corner shore is a triangular wedge, not a twisted quad. Omitting
        // the fourth ground-level triangle avoids invalid interpolation in
        // the voxel border shader.
        if (highCount == 1)
        {
            if (heights.SouthWest >= HighThreshold)
            {
                AddTriangle(southWest, northWest, southEast, color);
                return;
            }

            if (heights.SouthEast >= HighThreshold)
            {
                AddTriangle(southEast, southWest, northEast, color);
                return;
            }

            if (heights.NorthEast >= HighThreshold)
            {
                AddTriangle(northEast, southEast, northWest, color);
                return;
            }

            AddTriangle(northWest, northEast, southWest, color);
            return;
        }

        AddQuad(
            southWest,
            southEast,
            northEast,
            northWest,
            color);
    }

    private void AddSide(
        Vector3Int neighbourPosition,
        VoxelType type,
        float fillHeight,
        Vector3 bottomA,
        Vector3 topA,
        Vector3 topB,
        Vector3 bottomB,
        Color color)
    {
        if (!TryGetSideBottom(
                neighbourPosition,
                type,
                fillHeight,
                out float neighbourHeight))
        {
            return;
        }

        bottomA.y += neighbourHeight;
        bottomB.y += neighbourHeight;

        bool firstEdgeCollapsed =
            topA.y <= bottomA.y + 0.001f;
        bool secondEdgeCollapsed =
            topB.y <= bottomB.y + 0.001f;

        if (firstEdgeCollapsed && secondEdgeCollapsed)
        {
            return;
        }

        if (firstEdgeCollapsed)
        {
            AddTriangle(bottomA, bottomB, topB, color);
            return;
        }

        if (secondEdgeCollapsed)
        {
            AddTriangle(bottomA, topB, topA, color);
            return;
        }

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

    private void AddTriangle(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Color color)
    {
        if (Vector3.Cross(b - a, c - a).sqrMagnitude < 0.000001f)
        {
            return;
        }

        int index = vertices.Count;

        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);

        colors.Add(color);
        colors.Add(color);
        colors.Add(color);

        triangles.Add(index);
        triangles.Add(index + 1);
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
