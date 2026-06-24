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

    // -------------------------------------------------
    // Shared level anchor (single source of truth)
    // -------------------------------------------------
    private Vector3 levelCenter;

    private void Start()
    {
        activeMode = orbitMode;

        activeMode.Enter();

        currentState = CameraState.Orbit;

        orbitMode.UpdateCamera();

        // IMPORTANT: centralize level reference once
        levelCenter = LevelBoundsUtility.CalculateCenter(
            voxelWorld.GetChunkCoordinates(),
            Chunk.ChunkSize);

        orbitMode.SetOrbitCenter(levelCenter);
        puzzleMode.SetLevelCenter(levelCenter);

        //VoxelVisibilitySystem.SetView(SliceAxis.X, +1);
        //VoxelVisibilitySystem.SetVisibleLayer(0);
    }

    private void Update()
    {
        HandleTab();

        if (!isTransitioning)
        {
            activeMode?.HandleInput();
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

    private void HandleTab()
    {
        if (!Keyboard.current.tabKey.wasPressedThisFrame)
            return;

        if (currentState == CameraState.Orbit)
        {
            BeginTransitionToPuzzle();
        }
        else if (currentState == CameraState.Puzzle)
        {
            BeginTransitionToOrbit();
        }
        else if (currentState == CameraState.Transition)
        {
            ReverseTransition();
        }
    }

    // -------------------------------------------------
    // SIDE DETECTION
    // -------------------------------------------------
    private PuzzleSide DetermineClosestSide()
    {
        Vector3 offset = transform.position - levelCenter;
        offset.y = 0f;

        if (offset.sqrMagnitude < 0.0001f)
            return PuzzleSide.North;

        offset.Normalize();

        float east = Vector3.Dot(offset, Vector3.right);
        float west = Vector3.Dot(offset, Vector3.left);
        float north = Vector3.Dot(offset, Vector3.forward);
        float south = Vector3.Dot(offset, Vector3.back);

        float max = east;
        PuzzleSide side = PuzzleSide.East;

        if (west > max) { max = west; side = PuzzleSide.West; }
        if (north > max) { max = north; side = PuzzleSide.North; }
        if (south > max) { max = south; side = PuzzleSide.South; }

        return side;
    }

    // -------------------------------------------------
    // TRANSITIONS
    // -------------------------------------------------
    private void BeginTransitionToPuzzle()
    {
        orbitMode.Exit();

        orbitReturnPosition = transform.position;
        orbitReturnRotation = transform.rotation;

        PuzzleSide side = DetermineClosestSide();
        puzzleMode.SetSide(side);

        PuzzleSliceMapping.GetSlice(side, out SliceAxis axis, out int sign);

        VoxelVisibilitySystem.SetView(axis, sign);

        transitionStartPos = transform.position;
        transitionStartRot = transform.rotation;

        transitionTargetPos = puzzleMode.GetPuzzlePosition(side);
        transitionTargetRot = puzzleMode.GetPuzzleRotation(side);

        transitionDestination = CameraState.Puzzle;

        transitionProgress = 0f;

        currentState = CameraState.Transition;
        isTransitioning = true;
    }

    private void BeginTransitionToOrbit()
    {
        transitionStartPos = transform.position;
        transitionStartRot = transform.rotation;

        PuzzleSide side = DetermineClosestSide();

        puzzleMode.SetSide(side);

        transitionStartPos = transform.position;
        transitionStartRot = transform.rotation;

        transitionTargetPos = orbitReturnPosition;
        transitionTargetRot = orbitReturnRotation;

        transitionDestination = CameraState.Orbit;

        transitionProgress = 0f;

        currentState = CameraState.Transition;
        isTransitioning = true;
    }

    private void ReverseTransition()
    {
        (transitionStartPos, transitionTargetPos) =
            (transitionTargetPos, transitionStartPos);

        (transitionStartRot, transitionTargetRot) =
            (transitionTargetRot, transitionStartRot);

        transitionProgress = 1f - transitionProgress;

        transitionDestination =
            transitionDestination == CameraState.Puzzle
                ? CameraState.Orbit
                : CameraState.Puzzle;
    }

    private void UpdateTransition()
    {
        transitionProgress += Time.deltaTime * transitionSpeed;

        float t = Mathf.Clamp01(transitionProgress);

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
        isTransitioning = false;

        activeMode.Exit();

        if (transitionDestination == CameraState.Puzzle)
        {
            activeMode = puzzleMode;
            currentState = CameraState.Puzzle;
        }
        else
        {
            activeMode = orbitMode;
            currentState = CameraState.Orbit;

            VoxelVisibilitySystem.ResetVisibility();
            ChunkRefreshSystem.RequestFullRefresh();
        }

        activeMode.Enter();
    }

    // -------------------------------------------------
    // TEMP helper: replace later with proper chunk registry
    // -------------------------------------------------
    private Transform[] FindChunkRoots()
    {
        ChunkRenderer[] chunks =
            FindObjectsByType<ChunkRenderer>(
                FindObjectsSortMode.None);

        Transform[] result = new Transform[chunks.Length];

        for (int i = 0; i < chunks.Length; i++)
            result[i] = chunks[i].transform;

        return result;
    }
}