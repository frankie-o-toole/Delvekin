using UnityEngine;

[RequireComponent(typeof(DwarfAgent))]
[RequireComponent(typeof(DwarfJobController))]
public class DwarfMovement : MonoBehaviour
{
    public enum MovementState
    {
        Idle,
        Walking,
        SteppingUp,
        SteppingDown,
        ClimbingUp,
        ClimbingDown,
        LadderTransition,
        Turning,
        Falling
    }

    private enum LadderTraversalPhase
    {
        None,
        Ascending,
        Descending,
        EnteringDescent,
        ExitingTop
    }

    [Header("Walking")]
    [SerializeField]
    private float moveSpeed = 2f;

    [Header("Steps")]
    [SerializeField]
    private AnimationCurve stepVerticalCurve =
        AnimationCurve.EaseInOut(
            0f, 0f,
            1f, 1f);

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

    [Header("Water")]
    [SerializeField]
    [Range(0.1f, 1f)]
    private float wadingSpeedMultiplier = 0.6f;

    [SerializeField]
    [Range(0.1f, 1f)]
    private float deepWaterSpeedMultiplier = 0.4f;

    [SerializeField]
    [Min(1f)]
    private float drowningDistance = 12f;

    [SerializeField]
    [Min(1)]
    private int riverCentreScanDistance = 12;

    private VoxelWorld world;
    private DwarfPool pool;

    private DwarfAgent agent;
    private DwarfJobController jobController;

    private MovementState state =
        MovementState.Idle;

    private float moveProgress;
    private float currentFallSpeed;
    private float currentMoveSpeedMultiplier = 1f;
    private float deepWaterDistanceTravelled;

    private int fallStartY;

    private Vector3 startWorldPosition;
    private Vector3 targetWorldPosition;

    private Vector3Int pendingTargetVoxel;

    private Quaternion turnTargetRotation;

    private LadderTraversalPhase ladderPhase;
    private PuzzleSide ladderOutwardSide;
    private Vector3Int ladderTransitionDirection;
    private int ladderTransitionStepsRemaining;

    public MovementState State =>
        state;

    public bool IsMoving =>
        state != MovementState.Idle;

    public int FatalFallDistance =>
        fatalFallDistance;

    private void Awake()
    {
        agent =
            GetComponent<DwarfAgent>();

        jobController =
            GetComponent<DwarfJobController>();

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
            agent.IsFrozen ||
            world == null)
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
            case MovementState.ClimbingUp:
            case MovementState.ClimbingDown:
            case MovementState.LadderTransition:
                UpdateGroundMovement();
                break;
        }
    }

    private void DecideNextMove()
    {
        if (TryReachExit())
        {
            return;
        }

        if (ladderPhase !=
            LadderTraversalPhase.None)
        {
            ContinueLadderTraversal();
            return;
        }

        // Construction jobs may deliberately hold a dwarf beside terrain
        // without ordinary ground support. Let the active job own that state
        // before applying the normal falling rule.
        if (jobController != null &&
            jobController.ControlsMovement)
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

        if (TryBeginWaterCurrentMove())
        {
            return;
        }

        Vector3Int direction =
            DirectionUtility.ToVector(
                agent.Facing);

        Vector3Int forwardAnchor =
            agent.CurrentVoxel +
            direction;

        if (DirectionAltererRegistry.TryGetRedirect(
                agent,
                forwardAnchor,
                out PuzzleSide outputDirection))
        {
            BeginTurnTo(
                outputDirection);

            return;
        }

        if (jobController != null &&
            jobController.TryHandleMovementDecision())
        {
            return;
        }

        if (jobController != null &&
            jobController.ControlsMovement)
        {
            return;
        }

        PuzzleSide ladderSideForAscent =
            DirectionUtility.Opposite(
                agent.Facing);

        if (DwarfWorldQueries.HasClimbableLadderContact(
                world,
                agent.CurrentVoxel,
                ladderSideForAscent))
        {
            ladderOutwardSide =
                ladderSideForAscent;

            ladderPhase =
                LadderTraversalPhase.Ascending;

            ContinueLadderTraversal();
            return;
        }


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

            if (TryBeginLadderDescent(
                    direction))
            {
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
            moveSpeed *
            currentMoveSpeedMultiplier;

        float progress =
            Mathf.Clamp01(
                moveProgress);

        Vector3 position =
            Vector3.Lerp(
                startWorldPosition,
                targetWorldPosition,
                progress);

        if (state == MovementState.SteppingUp ||
            state == MovementState.SteppingDown ||
            state == MovementState.ClimbingUp ||
            state == MovementState.ClimbingDown ||
            state == MovementState.LadderTransition)
        {
            float verticalProgress =
                stepVerticalCurve.Evaluate(
                    progress);

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
        MovementState completedState =
            state;

        transform.position =
            targetWorldPosition;

        agent.SetCurrentVoxel(
            pendingTargetVoxel);

        float travelledDistance =
            Vector3.Distance(
                startWorldPosition,
                targetWorldPosition);

        if (UpdateWaterExposure(travelledDistance))
        {
            return;
        }

        moveProgress = 0f;
        state = MovementState.Idle;

        if (TryReachExit())
        {
            return;
        }

        if (completedState ==
                MovementState.ClimbingUp ||
            completedState ==
                MovementState.ClimbingDown ||
            completedState ==
                MovementState.LadderTransition)
        {
            return;
        }

        // A job must not activate in mid-air. It remains pending
        // throughout the fall and is tested again after landing.
        if (DwarfWorldQueries.HasNoSupport(
                world,
                agent.CurrentVoxel))
        {
            BeginFall();
            return;
        }

        if (jobController != null)
        {
            jobController.ActivatePendingJob();
        }
    }

    private void BeginTurnAround()
    {
        BeginTurnTo(
            DirectionUtility.Opposite(
                agent.Facing));
    }

    private void BeginTurnTo(
        PuzzleSide newFacing)
    {
        agent.SetFacing(
            newFacing,
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
                turnTargetRotation) > 0.1f)
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

        BeginFallToVoxel(
            agent.CurrentVoxel +
            Vector3Int.down);
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
                currentFallSpeed * Time.deltaTime);

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

        BeginFallToVoxel(
            agent.CurrentVoxel +
            Vector3Int.down);
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

        if (TryReachExit())
        {
            return;
        }

        currentFallSpeed = 0f;
        state = MovementState.Idle;

        if (jobController != null)
        {
            jobController.ActivatePendingJob();
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
        currentMoveSpeedMultiplier =
            GetWaterSpeedMultiplier(
                targetVoxel,
                movementState);
        state = movementState;
    }

    private bool TryBeginWaterCurrentMove()
    {
        Vector3Int currentDirection =
            world.GetFluidFlowDirection(
                agent.CurrentVoxel);

        if (currentDirection == Vector3Int.zero)
        {
            return false;
        }

        // Horizontal rivers gently centre a dwarf before advancing it.
        // This keeps its 3x3 footprint away from the banks without snapping.
        Vector3Int movementDirection =
            GetRiverCentreCorrection(currentDirection);

        if (movementDirection == Vector3Int.zero)
        {
            movementDirection = currentDirection;
        }

        if (movementDirection.y < 0)
        {
            Vector3Int below =
                agent.CurrentVoxel + Vector3Int.down;

            if (DwarfWorldQueries.CanOccupy(world, below))
            {
                MoveToVoxel(
                    below,
                    MovementState.SteppingDown);
                return true;
            }

            return false;
        }

        if (movementDirection.y != 0)
        {
            return false;
        }

        Vector3Int target =
            agent.CurrentVoxel +
            movementDirection;

        if (DwarfWorldQueries.CanOccupy(world, target))
        {
            if (DwarfWorldQueries.HasAnySupport(world, target))
            {
                SetFacingToDirection(movementDirection);
                MoveToVoxel(target, MovementState.Walking);
                return true;
            }

            Vector3Int stepDown =
                target + Vector3Int.down;

            if (DwarfWorldQueries.CanOccupy(world, stepDown) &&
                DwarfWorldQueries.HasAnySupport(world, stepDown))
            {
                SetFacingToDirection(movementDirection);
                MoveToVoxel(
                    stepDown,
                    MovementState.SteppingDown);
                return true;
            }

            SetFacingToDirection(movementDirection);
            MoveToVoxel(target, MovementState.Walking);
            return true;
        }

        if (DwarfWorldQueries.IsOneVoxelRise(world, target))
        {
            Vector3Int stepUp =
                target + Vector3Int.up;

            if (DwarfWorldQueries.CanOccupy(world, stepUp) &&
                DwarfWorldQueries.HasAnySupport(world, stepUp))
            {
                SetFacingToDirection(movementDirection);
                MoveToVoxel(
                    stepUp,
                    MovementState.SteppingUp);
                return true;
            }
        }

        // Keep the previous facing when the 3x3x5 footprint cannot take
        // the current yet. Ordinary movement can advance toward the corner
        // until the turn is spatially valid, or apply shoreline turn-around.
        return false;
    }

    private Vector3Int GetRiverCentreCorrection(
        Vector3Int flowDirection)
    {
        if (flowDirection.y != 0)
        {
            return Vector3Int.zero;
        }

        Vector3Int right =
            new(flowDirection.z, 0, -flowDirection.x);

        int rightWater =
            CountWaterCells(right);
        int leftWater =
            CountWaterCells(-right);

        // A difference of one still places the 3-wide footprint acceptably
        // close to centre. Correct only larger, clearly readable offsets.
        if (Mathf.Abs(rightWater - leftWater) < 2)
        {
            return Vector3Int.zero;
        }

        Vector3Int correction =
            rightWater > leftWater
                ? right
                : -right;

        Vector3Int target =
            agent.CurrentVoxel + correction;

        return HasWaterAcrossFootprint(target, flowDirection)
            ? correction
            : Vector3Int.zero;
    }

    private int CountWaterCells(Vector3Int direction)
    {
        int count = 0;

        for (int step = 1;
             step <= riverCentreScanDistance;
             step++)
        {
            Vector3Int position =
                agent.CurrentVoxel + direction * step;

            if (world.GetVoxel(position).Type != VoxelType.Water)
            {
                break;
            }

            count++;
        }

        return count;
    }

    private bool HasWaterAcrossFootprint(
        Vector3Int anchor,
        Vector3Int flowDirection)
    {
        Vector3Int lateral =
            new(flowDirection.z, 0, -flowDirection.x);

        for (int offset = -DwarfSpatialRules.HalfWidth;
             offset <= DwarfSpatialRules.HalfWidth;
             offset++)
        {
            if (world.GetVoxel(anchor + lateral * offset).Type !=
                VoxelType.Water)
            {
                return false;
            }
        }

        return true;
    }

    private void SetFacingToDirection(Vector3Int direction)
    {
        PuzzleSide facing = direction switch
        {
            var value when value == Vector3Int.forward =>
                PuzzleSide.North,
            var value when value == Vector3Int.right =>
                PuzzleSide.East,
            var value when value == Vector3Int.back =>
                PuzzleSide.South,
            var value when value == Vector3Int.left =>
                PuzzleSide.West,
            _ => agent.Facing
        };

        if (facing != agent.Facing)
        {
            agent.SetFacing(facing);
        }
    }

    private float GetWaterSpeedMultiplier(
        Vector3Int targetVoxel,
        MovementState movementState)
    {
        if (movementState == MovementState.ClimbingUp ||
            movementState == MovementState.ClimbingDown ||
            movementState == MovementState.LadderTransition)
        {
            return 1f;
        }

        float depth = Mathf.Max(
            GetWaterDepth(agent.CurrentVoxel),
            GetWaterDepth(targetVoxel));

        if (depth < 2.5f)
        {
            return 1f;
        }

        return depth < DwarfSpatialRules.Height
            ? wadingSpeedMultiplier
            : deepWaterSpeedMultiplier;
    }

    private float GetWaterDepth(Vector3Int anchor)
    {
        float depth = 0f;

        for (int y = 0;
             y < DwarfSpatialRules.Height;
             y++)
        {
            Vector3Int position =
                anchor + Vector3Int.up * y;

            if (world.GetVoxel(position).Type != VoxelType.Water)
            {
                break;
            }

            depth += world.GetFluidFill01(position);
        }

        return depth;
    }

    private bool UpdateWaterExposure(float travelledDistance)
    {
        float depth =
            GetWaterDepth(agent.CurrentVoxel);

        if (depth < DwarfSpatialRules.Height ||
            agent.HasFlotation)
        {
            deepWaterDistanceTravelled = 0f;
            return false;
        }

        deepWaterDistanceTravelled += travelledDistance;

        if (deepWaterDistanceTravelled < drowningDistance)
        {
            return false;
        }

        Debug.Log(
            $"{agent.name} drowned after travelling " +
            $"{deepWaterDistanceTravelled:0.0} voxel(s) in deep water.");

        Die();
        return true;
    }

    /// <summary>
    /// Hands movement back from a completed Ladder Builder to the
    /// ordinary ladder traversal state. The dwarf exits over the top
    /// when possible; otherwise it returns down the ladder.
    /// </summary>
    public void FinishLadderBuilding(
        PuzzleSide outwardSide)
    {
        ladderOutwardSide =
            outwardSide;

        ladderPhase =
            LadderTraversalPhase.Ascending;
    }

    private bool TryBeginLadderDescent(
        Vector3Int forward)
    {
        PuzzleSide outwardSide =
            agent.Facing;

        Vector3Int firstTransitionAnchor =
            agent.CurrentVoxel +
            forward;

        Vector3Int ladderSideAnchor =
            agent.CurrentVoxel +
            forward * 2;

        if (!DwarfWorldQueries.CanOccupy(
                world,
                firstTransitionAnchor) ||
            !DwarfWorldQueries.CanOccupy(
                world,
                ladderSideAnchor) ||
            !DwarfWorldQueries.HasClimbableLadderContact(
                world,
                ladderSideAnchor,
                outwardSide,
                verticalOffset: -1))
        {
            return false;
        }

        ladderOutwardSide =
            outwardSide;

        ladderPhase =
            LadderTraversalPhase.EnteringDescent;

        BeginLadderTransition(
            forward,
            stepCount: 2);

        return true;
    }

    private void ContinueLadderTraversal()
    {
        switch (ladderPhase)
        {
            case LadderTraversalPhase.Ascending:
                ContinueLadderAscent();
                break;

            case LadderTraversalPhase.Descending:
                ContinueLadderDescent();
                break;

            case LadderTraversalPhase.EnteringDescent:
            case LadderTraversalPhase.ExitingTop:
                ContinueLadderTransition();
                break;
        }
    }

    private void ContinueLadderAscent()
    {
        Vector3Int upwardAnchor =
            agent.CurrentVoxel +
            Vector3Int.up;

        if (DwarfWorldQueries.HasClimbableLadderContact(
                world,
                agent.CurrentVoxel,
                ladderOutwardSide) &&
            DwarfWorldQueries.CanOccupy(
                world,
                upwardAnchor))
        {
            MoveToVoxel(
                upwardAnchor,
                MovementState.ClimbingUp);

            return;
        }

        Vector3Int inward =
            -DirectionUtility.ToVector(
                ladderOutwardSide);

        if (CanCompleteLadderTopExit(
                inward))
        {
            ladderPhase =
                LadderTraversalPhase.ExitingTop;

            BeginLadderTransition(
                inward,
                stepCount: 2);

            return;
        }

        Vector3Int downwardAnchor =
            agent.CurrentVoxel +
            Vector3Int.down;

        if (DwarfWorldQueries.HasClimbableLadderContact(
                world,
                agent.CurrentVoxel,
                ladderOutwardSide,
                verticalOffset: -1) &&
            DwarfWorldQueries.CanOccupy(
                world,
                downwardAnchor))
        {
            // A failed ascent returns to the side from which it came.
            agent.SetFacing(
                ladderOutwardSide);

            ladderPhase =
                LadderTraversalPhase.Descending;

            MoveToVoxel(
                downwardAnchor,
                MovementState.ClimbingDown);

            return;
        }

        ladderPhase =
            LadderTraversalPhase.None;

        BeginFall();
    }

    private void ContinueLadderDescent()
    {
        Vector3Int downwardAnchor =
            agent.CurrentVoxel +
            Vector3Int.down;

        if (DwarfWorldQueries.HasClimbableLadderContact(
                world,
                agent.CurrentVoxel,
                ladderOutwardSide,
                verticalOffset: -1) &&
            DwarfWorldQueries.CanOccupy(
                world,
                downwardAnchor))
        {
            MoveToVoxel(
                downwardAnchor,
                MovementState.ClimbingDown);

            return;
        }

        ladderPhase =
            LadderTraversalPhase.None;

        // At the bottom the dwarf is already beside the ladder.
        // Its outward facing lets ordinary walking carry it away.
    }

    private bool CanCompleteLadderTopExit(
        Vector3Int inward)
    {
        Vector3Int firstAnchor =
            agent.CurrentVoxel +
            inward;

        Vector3Int finalAnchor =
            agent.CurrentVoxel +
            inward * 2;

        return
            DwarfWorldQueries.CanOccupy(
                world,
                firstAnchor) &&
            DwarfWorldQueries.CanOccupy(
                world,
                finalAnchor) &&
            DwarfWorldQueries.HasAnySupport(
                world,
                finalAnchor);
    }

    private void BeginLadderTransition(
        Vector3Int direction,
        int stepCount)
    {
        ladderTransitionDirection =
            direction;

        ladderTransitionStepsRemaining =
            Mathf.Max(
                0,
                stepCount);

        ContinueLadderTransition();
    }

    private void ContinueLadderTransition()
    {
        if (ladderTransitionStepsRemaining > 0)
        {
            Vector3Int targetAnchor =
                agent.CurrentVoxel +
                ladderTransitionDirection;

            ladderTransitionStepsRemaining--;

            MoveToVoxel(
                targetAnchor,
                MovementState.LadderTransition);

            return;
        }

        if (ladderPhase ==
            LadderTraversalPhase.EnteringDescent)
        {
            ladderPhase =
                LadderTraversalPhase.Descending;

            ContinueLadderDescent();
            return;
        }

        ladderPhase =
            LadderTraversalPhase.None;
    }

    private void Die()
    {
        Debug.Log(
            $"{agent.name} died!");

        state = MovementState.Idle;

        if (pool != null)
        {
            pool.Release(
                agent,
                DwarfReleaseReason.Died);
            return;
        }

        agent.Deactivate();
    }

    private bool TryReachExit()
    {
        if (world.GetVoxel(agent.CurrentVoxel).Type !=
            VoxelType.ExitPoint)
        {
            return false;
        }

        Debug.Log(
            $"{agent.name} reached the exit!");

        state = MovementState.Idle;

        if (pool != null)
        {
            pool.Release(
                agent,
                DwarfReleaseReason.Rescued);
        }
        else
        {
            agent.Deactivate();
        }

        return true;
    }

    private void ResetMovementState()
    {
        state = MovementState.Idle;

        moveProgress = 0f;
        currentFallSpeed = 0f;
        currentMoveSpeedMultiplier = 1f;
        deepWaterDistanceTravelled = 0f;

        startWorldPosition =
            transform.position;

        targetWorldPosition =
            transform.position;

        pendingTargetVoxel = default;
        turnTargetRotation = Quaternion.identity;
        fallStartY = 0;

        ladderPhase =
            LadderTraversalPhase.None;

        ladderOutwardSide =
            PuzzleSide.North;

        ladderTransitionDirection =
            Vector3Int.zero;

        ladderTransitionStepsRemaining = 0;
    }
}
