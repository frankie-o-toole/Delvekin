using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraStateController : MonoBehaviour
{
    [SerializeField] private OrbitCameraMode orbitMode;
    [SerializeField] private PuzzleCameraMode puzzleMode;
    [SerializeField] private VoxelWorld voxelWorld;

    [SerializeField] private float transitionSpeed = 3f;

    private ICameraMode activeMode;

    private CameraState currentState;

    private bool isTransitioning;

    private Vector3 transitionStartPos;
    private Quaternion transitionStartRot;

    private Vector3 transitionTargetPos;
    private Quaternion transitionTargetRot;

    private CameraState transitionDestination;

    private float transitionProgress;

    private Vector3 orbitReturnPosition;
    private Quaternion orbitReturnRotation;

    private Vector3 levelCenter;

    private void Start()
    {
        activeMode = orbitMode;

        activeMode.Enter();

        currentState = CameraState.Orbit;

        Vector3 initialCenter =
            LevelBoundsUtility.CalculateCenter(
                voxelWorld.GetChunkCoordinates(),
                Chunk.ChunkSize);

        SetLevelCenter(initialCenter);

        orbitMode.UpdateCamera();
    }

    private void Update()
    {
        HandleQuit();
        HandleTab();

        if (!isTransitioning)
        {
            activeMode?.HandleInput();
        }
    }

    private void HandleQuit()
    {
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Application.Quit();
        }
    }

    private void LateUpdate()
    {
        if (isTransitioning)
        {
            UpdateTransition();
            return;
        }

        activeMode?.UpdateCamera();
    }

    // =====================================================
    // LEVEL CENTER
    // =====================================================

    public void SetLevelCenter(
    Vector3 newCenter,
    bool recenterOrbit = true)
    {
        Vector3 centerDelta =
            newCenter - levelCenter;

        levelCenter =
            newCenter;

        if (recenterOrbit)
        {
            orbitMode.SetOrbitCenter(
                levelCenter);

            if (currentState != CameraState.Orbit)
            {
                orbitReturnPosition +=
                    centerDelta;
            }
        }

        // Puzzle mode still needs current occupied bounds,
        // even when the Orbit camera must remain untouched.
        if (TryCalculateOccupiedBounds(
                out Bounds occupiedBounds))
        {
            puzzleMode.SetOccupiedBounds(
                occupiedBounds);

            if (recenterOrbit)
            {
                orbitMode.FrameBounds(
                    occupiedBounds);
            }
        }
    }

    // =====================================================
    // OCCUPIED VOXEL BOUNDS
    // =====================================================

    private bool TryCalculateOccupiedBounds(
        out Bounds occupiedBounds)
    {
        bool foundVoxel = false;

        Vector3 min =
            Vector3.zero;

        Vector3 max =
            Vector3.zero;

        IEnumerable<Vector3Int> chunkCoordinates =
            voxelWorld.GetChunkCoordinates();

        foreach (
            Vector3Int chunkCoord
            in chunkCoordinates)
        {
            Vector3Int chunkOrigin =
                chunkCoord *
                Chunk.ChunkSize;

            for (
                int x = 0;
                x < Chunk.ChunkSize;
                x++)
            {
                for (
                    int y = 0;
                    y < Chunk.ChunkSize;
                    y++)
                {
                    for (
                        int z = 0;
                        z < Chunk.ChunkSize;
                        z++)
                    {
                        Vector3Int worldPos =
                            chunkOrigin +
                            new Vector3Int(
                                x,
                                y,
                                z);

                        Voxel voxel =
                            voxelWorld.GetVoxel(
                                worldPos);

                        if (
                            voxel.Type ==
                            VoxelType.Air)
                        {
                            continue;
                        }

                        // A voxel occupies:
                        //
                        // worldPos
                        // through
                        // worldPos + (1,1,1)
                        //
                        // So bounds describe actual
                        // voxel surfaces, not centers.
                        Vector3 voxelMin =
                            worldPos;

                        Vector3 voxelMax =
                            (Vector3)worldPos +
                            Vector3.one;

                        if (!foundVoxel)
                        {
                            min =
                                voxelMin;

                            max =
                                voxelMax;

                            foundVoxel =
                                true;
                        }
                        else
                        {
                            min =
                                Vector3.Min(
                                    min,
                                    voxelMin);

                            max =
                                Vector3.Max(
                                    max,
                                    voxelMax);
                        }
                    }
                }
            }
        }

        if (!foundVoxel)
        {
            occupiedBounds =
                new Bounds(
                    Vector3.zero,
                    Vector3.zero);

            return false;
        }

        Vector3 center =
            (min + max) *
            0.5f;

        Vector3 size =
            max - min;

        occupiedBounds =
            new Bounds(
                center,
                size);

        return true;
    }

    // =====================================================
    // MODE SWITCHING
    // =====================================================

    private void HandleTab()
    {
        if (
            !Keyboard.current.tabKey
                .wasPressedThisFrame)
        {
            return;
        }

        if (
            currentState ==
            CameraState.Orbit)
        {
            BeginTransitionToPuzzle();
        }
        else if (
            currentState ==
            CameraState.Puzzle)
        {
            BeginTransitionToOrbit();
        }
        else if (
            currentState ==
            CameraState.Transition)
        {
            ReverseTransition();
        }
    }

    // =====================================================
    // SIDE DETECTION
    // =====================================================

    private PuzzleSide DetermineClosestSide()
    {
        Vector3 offset =
            transform.position -
            levelCenter;

        offset.y = 0f;

        if (
            offset.sqrMagnitude <
            0.0001f)
        {
            return PuzzleSide.North;
        }

        offset.Normalize();

        float east =
            Vector3.Dot(
                offset,
                Vector3.right);

        float west =
            Vector3.Dot(
                offset,
                Vector3.left);

        float north =
            Vector3.Dot(
                offset,
                Vector3.forward);

        float south =
            Vector3.Dot(
                offset,
                Vector3.back);

        float max =
            east;

        PuzzleSide side =
            PuzzleSide.East;

        if (west > max)
        {
            max =
                west;

            side =
                PuzzleSide.West;
        }

        if (north > max)
        {
            max =
                north;

            side =
                PuzzleSide.North;
        }

        if (south > max)
        {
            side =
                PuzzleSide.South;
        }

        return side;
    }

    // =====================================================
    // TRANSITION TO PUZZLE
    // =====================================================

    private void BeginTransitionToPuzzle()
    {
        orbitMode.Exit();

        orbitReturnPosition =
            transform.position;

        orbitReturnRotation =
            transform.rotation;

        PuzzleSide side =
            DetermineClosestSide();

        // Refresh occupied bounds immediately
        // before calculating the Puzzle camera.
        if (TryCalculateOccupiedBounds(
                out Bounds occupiedBounds))
        {
            puzzleMode.SetOccupiedBounds(
                occupiedBounds);
        }

        puzzleMode.SetSide(
            side);

        PuzzleSliceMapping.GetSlice(
            side,
            out SliceAxis axis,
            out int sign);

        VoxelVisibilitySystem.SetView(
            axis,
            sign);

        DwarfVisibilitySystem.SetView(
            axis,
            sign);

        transitionStartPos =
            transform.position;

        transitionStartRot =
            transform.rotation;

        transitionTargetPos =
            puzzleMode.GetPuzzlePosition(
                side);

        transitionTargetRot =
            puzzleMode.GetPuzzleRotation(
                side);

        transitionDestination =
            CameraState.Puzzle;

        transitionProgress =
            0f;

        currentState =
            CameraState.Transition;

        isTransitioning =
            true;
    }

    // =====================================================
    // TRANSITION TO ORBIT
    // =====================================================

    private void BeginTransitionToOrbit()
    {
        transitionStartPos =
            transform.position;

        transitionStartRot =
            transform.rotation;

        PuzzleSide side =
            DetermineClosestSide();

        puzzleMode.SetSide(
            side);

        transitionTargetPos =
            orbitReturnPosition;

        transitionTargetRot =
            orbitReturnRotation;

        transitionDestination =
            CameraState.Orbit;

        transitionProgress =
            0f;

        currentState =
            CameraState.Transition;

        isTransitioning =
            true;
    }

    private void ReverseTransition()
    {
        (
            transitionStartPos,
            transitionTargetPos
        ) =
        (
            transitionTargetPos,
            transitionStartPos
        );

        (
            transitionStartRot,
            transitionTargetRot
        ) =
        (
            transitionTargetRot,
            transitionStartRot
        );

        transitionProgress =
            1f -
            transitionProgress;

        transitionDestination =
            transitionDestination ==
            CameraState.Puzzle
                ? CameraState.Orbit
                : CameraState.Puzzle;
    }

    private void UpdateTransition()
    {
        transitionProgress +=
            Time.deltaTime *
            transitionSpeed;

        float t =
            Mathf.Clamp01(
                transitionProgress);

        transform.position =
            Vector3.Lerp(
                transitionStartPos,
                transitionTargetPos,
                t);

        transform.rotation =
            Quaternion.Slerp(
                transitionStartRot,
                transitionTargetRot,
                t);

        if (t >= 1f)
        {
            FinishTransition();
        }
    }

    private void FinishTransition()
    {
        isTransitioning =
            false;

        activeMode.Exit();

        if (
            transitionDestination ==
            CameraState.Puzzle)
        {
            activeMode =
                puzzleMode;

            currentState =
                CameraState.Puzzle;
        }
        else
        {
            activeMode =
                orbitMode;

            currentState =
                CameraState.Orbit;

            VoxelVisibilitySystem
                .ResetVisibility();

            ChunkRefreshSystem
                .RequestFullRefresh();
        }

        activeMode.Enter();
    }

    // =====================================================
    // TEMP HELPER
    // =====================================================

    private Transform[] FindChunkRoots()
    {
        ChunkRenderer[] chunks =
            FindObjectsByType<ChunkRenderer>(
                FindObjectsSortMode.None);

        Transform[] result =
            new Transform[
                chunks.Length];

        for (
            int i = 0;
            i < chunks.Length;
            i++)
        {
            result[i] =
                chunks[i].transform;
        }

        return result;
    }
}
