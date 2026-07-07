using UnityEngine;

[RequireComponent(typeof(DwarfAgent))]
public class DwarfMovement : MonoBehaviour
{
    public enum MovementState
    {
        Idle,
        Walking,
        Falling
    }

    [SerializeField] private float moveSpeed = 2f;

    private VoxelWorld world;
    private DwarfAgent agent;

    private MovementState state;

    private float moveProgress;

    private int fallStartY;

    private Vector3 startWorldPos;
    private Vector3 targetWorldPos;

    private Vector3Int pendingTargetVoxel;

    private void Awake()
    {
        agent = GetComponent<DwarfAgent>();
        world = FindFirstObjectByType<VoxelWorld>();

        state = MovementState.Idle;
    }

    private void Update()
    {
        if (state == MovementState.Walking ||
            state == MovementState.Falling)
        {
            UpdateMovement();
            return;
        }

        DecideNextMove();
    }

    // --------------------------------------------------
    // CORE MOVEMENT (shared for walking + falling)
    // --------------------------------------------------
    private void UpdateMovement()
    {
        moveProgress += Time.deltaTime * moveSpeed;

        transform.position = Vector3.Lerp(
            startWorldPos,
            targetWorldPos,
            moveProgress);

        if (moveProgress < 1f)
            return;

        // snap at end
        transform.position = targetWorldPos;

        agent.SetCurrentVoxel(pendingTargetVoxel);
        moveProgress = 0f;

        // AFTER ARRIVAL: resolve gravity
        ResolvePostMove();

        if (state != MovementState.Falling)
        {
            state = MovementState.Idle;
        }
    }

    // --------------------------------------------------
    // DECISION PHASE
    // --------------------------------------------------
    private void DecideNextMove()
    {
        Vector3Int below = agent.CurrentVoxel + Vector3Int.down;

        // no support -> start falling sequence
        if (!world.HasSupport(below))
        {
            BeginFall(below);
            return;
        }

        Vector3Int forward =
            agent.CurrentVoxel +
            DirectionUtility.ToVector(agent.Facing);

        if (world.GetVoxel(forward).Type != VoxelType.Air)
        {
            TurnAround();
            return;
        }

        MoveToVoxel(forward, MovementState.Walking);
    }

    // --------------------------------------------------
    // FALL START
    // --------------------------------------------------
    private void BeginFall(Vector3Int fallTarget)
    {
        fallStartY = agent.CurrentVoxel.y;

        state = MovementState.Falling;

        MoveToVoxel(fallTarget, MovementState.Falling);
    }

    // --------------------------------------------------
    // AFTER EACH MOVE (critical gravity logic)
    // --------------------------------------------------
    private void ResolvePostMove()
    {
        Vector3Int below = agent.CurrentVoxel + Vector3Int.down;

        // still standing on something
        if (world.HasSupport(below))
        {
            if (state == MovementState.Falling)
            {
                int fallDistance = fallStartY - agent.CurrentVoxel.y;

                Debug.Log($"Fall distance: {fallDistance}");

                if (fallDistance >= 5)
                {
                    Die();
                    return;
                }

                fallStartY = agent.CurrentVoxel.y;
            }

            state = MovementState.Idle;
            return;
        }

        // no support below -> gravity starts
        if (state != MovementState.Falling)
        {
            fallStartY = agent.CurrentVoxel.y;
        }

        MoveToVoxel(below, MovementState.Falling);
    }

    // --------------------------------------------------
    // TURNING
    // --------------------------------------------------
    private void TurnAround()
    {
        agent.SetFacing(DirectionUtility.Opposite(agent.Facing));
    }

    // --------------------------------------------------
    // MOVE REQUEST
    // --------------------------------------------------
    public void MoveToVoxel(Vector3Int targetVoxel, MovementState moveState)
    {
        pendingTargetVoxel = targetVoxel;

        startWorldPos = transform.position;
        targetWorldPos = VoxelToWorld(targetVoxel);

        moveProgress = 0f;
        state = moveState;
    }

    // --------------------------------------------------
    // DEATH
    // --------------------------------------------------
    private void Die()
    {
        Debug.Log("Dwarf died from fall damage");
        gameObject.SetActive(false);
    }

    // --------------------------------------------------
    // VOXEL -> WORLD
    // --------------------------------------------------
    private static Vector3 VoxelToWorld(Vector3Int voxel)
    {
        return new Vector3(
            voxel.x + 0.5f,
            voxel.y + 0.5f,
            voxel.z + 0.5f);
    }
}