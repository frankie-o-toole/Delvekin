using UnityEngine;
using UnityEngine.InputSystem;

public sealed class DwarfIslandCameraController : MonoBehaviour
{
    [Header("Hub Camera Modes")]
    [SerializeField]
    private OrbitCameraMode orbitMode;

    [SerializeField]
    private CityFacadeCameraMode cityFacadeMode;

    [Header("Transition")]
    [Min(0.01f)]
    [SerializeField]
    private float transitionDuration = 0.65f;

    [SerializeField]
    private AnimationCurve transitionCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private ICameraMode activeMode;
    private DwarfIslandCameraState currentState;
    private DwarfIslandCameraState transitionDestination;
    private CameraPose transitionStart;
    private CameraPose transitionTarget;
    private CameraPose orbitReturnPose;
    private float transitionElapsed;

    public DwarfIslandCameraState CurrentState => currentState;
    public bool IsTransitioning =>
        currentState == DwarfIslandCameraState.Transition;

    private void Start()
    {
        if (orbitMode == null || cityFacadeMode == null)
        {
            Debug.LogError(
                "DwarfIsland camera requires both Orbit and City Facade " +
                "camera modes.",
                this);
            enabled = false;
            return;
        }

        activeMode = orbitMode;
        currentState = DwarfIslandCameraState.Orbit;
        orbitMode.Enter();
        orbitMode.UpdateCamera();
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.tabKey.wasPressedThisFrame)
        {
            ToggleMode();
        }

        if (!IsTransitioning)
        {
            activeMode?.HandleInput();
        }
    }

    private void LateUpdate()
    {
        if (IsTransitioning)
        {
            UpdateTransition();
            return;
        }

        activeMode?.UpdateCamera();
    }

    public void ToggleMode()
    {
        if (!enabled || orbitMode == null || cityFacadeMode == null)
        {
            return;
        }

        if (IsTransitioning)
        {
            ReverseTransition();
            return;
        }

        if (currentState == DwarfIslandCameraState.Orbit)
        {
            BeginCityTransition();
        }
        else
        {
            BeginOrbitTransition();
        }
    }

    private void BeginCityTransition()
    {
        orbitReturnPose = CaptureCurrentPose();
        orbitMode.Exit();
        cityFacadeMode.ResetView();

        BeginTransition(
            cityFacadeMode.GetCurrentPose(),
            DwarfIslandCameraState.City);
    }

    private void BeginOrbitTransition()
    {
        cityFacadeMode.Exit();

        BeginTransition(
            orbitReturnPose,
            DwarfIslandCameraState.Orbit);
    }

    private void BeginTransition(
        CameraPose target,
        DwarfIslandCameraState destination)
    {
        transitionStart = CaptureCurrentPose();
        transitionTarget = target;
        transitionDestination = destination;
        transitionElapsed = 0f;
        currentState = DwarfIslandCameraState.Transition;
    }

    private void ReverseTransition()
    {
        (transitionStart, transitionTarget) =
            (transitionTarget, transitionStart);
        transitionElapsed = Mathf.Max(
            0f,
            transitionDuration - transitionElapsed);
        transitionDestination = transitionDestination ==
            DwarfIslandCameraState.City
                ? DwarfIslandCameraState.Orbit
                : DwarfIslandCameraState.City;
    }

    private void UpdateTransition()
    {
        transitionElapsed += Time.unscaledDeltaTime;
        float linearProgress = Mathf.Clamp01(
            transitionElapsed / Mathf.Max(0.01f, transitionDuration));
        float curvedProgress = transitionCurve == null
            ? linearProgress
            : transitionCurve.Evaluate(linearProgress);

        transform.SetPositionAndRotation(
            Vector3.Lerp(
                transitionStart.Position,
                transitionTarget.Position,
                curvedProgress),
            Quaternion.Slerp(
                transitionStart.Rotation,
                transitionTarget.Rotation,
                curvedProgress));

        if (linearProgress >= 1f)
        {
            FinishTransition();
        }
    }

    private void FinishTransition()
    {
        currentState = transitionDestination;
        activeMode = currentState == DwarfIslandCameraState.City
            ? cityFacadeMode
            : orbitMode;
        activeMode.Enter();
    }

    private CameraPose CaptureCurrentPose()
    {
        return new CameraPose(
            transform.position,
            transform.rotation);
    }

    private void OnValidate()
    {
        transitionDuration = Mathf.Max(0.01f, transitionDuration);
    }
}
