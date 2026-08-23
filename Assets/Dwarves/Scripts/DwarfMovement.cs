using UnityEngine;

[RequireComponent(typeof(DwarfAgent))]
[RequireComponent(typeof(DwarfAbilityController))]
public class DwarfMovement : MonoBehaviour
{
    public enum MovementState
    {
        Idle,
        Walking,
        SteppingUp,
        SteppingDown,
        Turning,
        Falling
    }

    [Header("Walking")]
    [SerializeField]
    private float moveSpeed = 2f;

    [Header("Steps")]
    [SerializeField]
    private AnimationCurve stepVerticalCurve =
        AnimationCurve.EaseInOut(
            0f,
            0f,
            1f,
            1f);

    [Header("Turning")]
    [SerializeField]
    private float turnSpeed = 540f;

    [Header("Falling")]
    [SerializeField]
    private float initialFallSpeed = 1.5f;

    [SerializeField]
    private float fallAcceleration = 12f;

    [SerializeField]
    private float maximumFallSpeed = 20f;

    [SerializeField]
    private int fatalFallDistance = 5;

    private VoxelWorld world;
    private DwarfPool pool;

    private DwarfAgent agent;
    private DwarfAbilityController abilityController;

    private MovementState state =
        MovementState.Idle;

    private float moveProgress;
    private float currentFallSpeed;

    private int fallStartY;

    private Vector3 startWorldPosition;
    private Vector3 targetWorldPosition;

    private Vector3Int pendingTargetVoxel;

    private Quaternion turnTargetRotation;

    public MovementState State =>
        state;

    public bool IsMoving =>
        state != MovementState.Idle;

    private void Awake()
    {
        agent =
            GetComponent<DwarfAgent>();

        abilityController =
            GetComponent<DwarfAbilityController>();

        world =
            FindFirstObjectByType<VoxelWorld>();

        pool =
            GetComponentInParent<DwarfPool>();
    }

    private void OnEnable()
    {
        ResetMovementState();
    }

    private void Update()
    {
        if (agent == null ||
            !agent.IsActive ||
            agent.IsFrozen)
        {
            return;
        }

        if (world == null)
        {
            return;
        }

        switch (state)
        {
            case MovementState.Idle:
                DecideNextMove();
                break;

            case MovementState.Turning:
                UpdateTurning();
                break;

            case MovementState.Falling:
                UpdateFalling();
                break;

            case MovementState.Walking:
            case MovementState.SteppingUp:
            case MovementState.SteppingDown:
                UpdateGroundMovement();
                break;
        }
    }

    private void DecideNextMove()
    {
        if (abilityController != null &&
            abilityController.ControlsMovement)
        {
            return;
        }

        if (DwarfWorldQueries.HasNoSupport(
                world,
                agent.CurrentVoxel))
        {
            BeginFall();
            return;
        }

        Vector3Int direction =
            DirectionUtility.ToVector(
                agent.Facing);

        Vector3Int forwardAnchor =
            agent.CurrentVoxel +
            direction;

        if (DwarfWorldQueries.CanOccupy(
                world,
                forwardAnchor))
        {
            if (DwarfWorldQueries.HasAnySupport(
                    world,
                    forwardAnchor))
            {
                MoveToVoxel(
                    forwardAnchor,
                    MovementState.Walking);

                return;
            }

            Vector3Int stepDownAnchor =
                forwardAnchor +
                Vector3Int.down;

            if (DwarfWorldQueries.CanOccupy(
                    world,
                    stepDownAnchor) &&
                DwarfWorldQueries.HasAnySupport(
                    world,
                    stepDownAnchor))
            {
                MoveToVoxel(
                    stepDownAnchor,
                    MovementState.SteppingDown);

                return;
            }

            MoveToVoxel(
                forwardAnchor,
                MovementState.Walking);

            return;
        }

        if (DwarfWorldQueries.IsOneVoxelRise(
                world,
                forwardAnchor))
        {
            Vector3Int stepUpAnchor =
                forwardAnchor +
                Vector3Int.up;

            if (DwarfWorldQueries.CanOccupy(
                    world,
                    stepUpAnchor) &&
                DwarfWorldQueries.HasAnySupport(
                    world,
                    stepUpAnchor))
            {
                MoveToVoxel(
                    stepUpAnchor,
                    MovementState.SteppingUp);

                return;
            }
        }

        BeginTurnAround();
    }

    private void UpdateGroundMovement()
    {
        moveProgress +=
            Time.deltaTime *
            moveSpeed;

        float normalizedProgress =
            Mathf.Clamp01(moveProgress);

        Vector3 position =
            Vector3.Lerp(
                startWorldPosition,
                targetWorldPosition,
                normalizedProgress);

        if (state == MovementState.SteppingUp ||
            state == MovementState.SteppingDown)
        {
            float verticalProgress =
                stepVerticalCurve.Evaluate(
                    normalizedProgress);

            position.y =
                Mathf.Lerp(
                    startWorldPosition.y,
                    targetWorldPosition.y,
                    verticalProgress);
        }

        transform.position =
            position;

        if (moveProgress >= 1f)
        {
            CompleteGroundMovement();
        }
    }

    private void CompleteGroundMovement()
    {
        transform.position =
            targetWorldPosition;

        agent.SetCurrentVoxel(
            pendingTargetVoxel);

        moveProgress = 0f;
        state = MovementState.Idle;

        if (abilityController != null)
        {
            abilityController.ActivatePendingAbility();
        }

        if (DwarfWorldQueries.HasNoSupport(
                world,
                agent.CurrentVoxel))
        {
            BeginFall();
        }
    }

    private void BeginTurnAround()
    {
        PuzzleSide opposite =
            DirectionUtility.Opposite(
                agent.Facing);

        agent.SetFacing(
            opposite,
            snapVisual: false);

        if (agent.VisualRoot == null)
        {
            agent.SnapVisualToFacing();
            state = MovementState.Idle;
            return;
        }

        turnTargetRotation =
            agent.GetFacingRotation();

        state =
            MovementState.Turning;
    }

    private void UpdateTurning()
    {
        if (agent.VisualRoot == null)
        {
            state = MovementState.Idle;
            return;
        }

        agent.VisualRoot.rotation =
            Quaternion.RotateTowards(
                agent.VisualRoot.rotation,
                turnTargetRotation,
                turnSpeed * Time.deltaTime);

        if (Quaternion.Angle(
                agent.VisualRoot.rotation,
                turnTargetRotation) >
            0.1f)
        {
            return;
        }

        agent.VisualRoot.rotation =
            turnTargetRotation;

        state =
            MovementState.Idle;
    }

    private void BeginFall()
    {
        if (state != MovementState.Falling)
        {
            fallStartY =
                agent.CurrentVoxel.y;

            currentFallSpeed =
                initialFallSpeed;
        }

        Vector3Int fallTarget =
            agent.CurrentVoxel +
            Vector3Int.down;

        BeginFallToVoxel(
            fallTarget);
    }

    private void BeginFallToVoxel(
        Vector3Int targetVoxel)
    {
        pendingTargetVoxel =
            targetVoxel;

        agent.SetTargetVoxel(
            targetVoxel);

        targetWorldPosition =
            DwarfSpatialRules
                .AnchorVoxelToRootPosition(targetVoxel);

        state =
            MovementState.Falling;
    }

    private void UpdateFalling()
    {
        currentFallSpeed +=
            fallAcceleration *
            Time.deltaTime;

        currentFallSpeed =
            Mathf.Min(
                currentFallSpeed,
                maximumFallSpeed);

        transform.position =
            Vector3.MoveTowards(
                transform.position,
                targetWorldPosition,
                currentFallSpeed *
                Time.deltaTime);

        if ((transform.position - targetWorldPosition)
            .sqrMagnitude > 0.0001f)
        {
            return;
        }

        CompleteFallCell();
    }

    private void CompleteFallCell()
    {
        transform.position =
            targetWorldPosition;

        agent.SetCurrentVoxel(
            pendingTargetVoxel);

        if (DwarfWorldQueries.HasAnySupport(
                world,
                agent.CurrentVoxel))
        {
            Land();
            return;
        }

        Vector3Int nextFallTarget =
            agent.CurrentVoxel +
            Vector3Int.down;

        BeginFallToVoxel(
            nextFallTarget);
    }

    private void Land()
    {
        int fallDistance =
            fallStartY -
            agent.CurrentVoxel.y;

        Debug.Log(
            $"{agent.name} landed after falling "
            + $"{fallDistance} voxel(s).");

        if (fallDistance >= fatalFallDistance)
        {
            Die();
            return;
        }

        currentFallSpeed = 0f;
        state = MovementState.Idle;

        if (abilityController != null)
        {
            abilityController.ActivatePendingAbility();
        }
    }

    public void MoveToVoxel(
        Vector3Int targetVoxel,
        MovementState movementState)
    {
        pendingTargetVoxel =
            targetVoxel;

        agent.SetTargetVoxel(
            targetVoxel);

        startWorldPosition =
            transform.position;

        targetWorldPosition =
            DwarfSpatialRules
                .AnchorVoxelToRootPosition(targetVoxel);

        moveProgress = 0f;
        state = movementState;
    }

    private void Die()
    {
        Debug.Log(
            $"{agent.name} died!");

        state = MovementState.Idle;

        if (pool != null)
        {
            pool.Release(agent);
            return;
        }

        agent.Deactivate();
    }

    private void ResetMovementState()
    {
        state = MovementState.Idle;

        moveProgress = 0f;
        currentFallSpeed = 0f;

        startWorldPosition =
            transform.position;

        targetWorldPosition =
            transform.position;

        pendingTargetVoxel = default;

        turnTargetRotation =
            Quaternion.identity;

        fallStartY = 0;
    }
}