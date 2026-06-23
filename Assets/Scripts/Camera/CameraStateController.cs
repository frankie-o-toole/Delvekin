using UnityEngine;
using UnityEngine.InputSystem;

public class CameraStateController : MonoBehaviour
{
    [SerializeField] private OrbitCameraMode orbitMode;
    [SerializeField] private PuzzleCameraMode puzzleMode;

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

    private void Start()
    {
        activeMode = orbitMode;

        activeMode.Enter();

        currentState = CameraState.Orbit;

        orbitMode.UpdateCamera();
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

    private void BeginTransitionToPuzzle()
    {
        orbitMode.Exit();

        orbitReturnPosition = transform.position;
        orbitReturnRotation = transform.rotation;

        transitionStartPos = transform.position;
        transitionStartRot = transform.rotation;

        transitionTargetPos = puzzleMode.GetPuzzlePosition();
        transitionTargetRot = puzzleMode.GetPuzzleRotation();

        transitionDestination = CameraState.Puzzle;

        transitionProgress = 0f;

        currentState = CameraState.Transition;
        isTransitioning = true;
    }

    private void BeginTransitionToOrbit()
    {
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
        }

        activeMode.Enter();
    }
}