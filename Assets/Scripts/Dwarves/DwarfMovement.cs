using UnityEngine;

[RequireComponent(typeof(DwarfAgent))]
public class DwarfMovement : MonoBehaviour
{
    private enum MovementState
    {
        Idle,
        Moving
    }

    [SerializeField] private float moveSpeed = 2f;

    private VoxelWorld world;
    private DwarfAgent agent;

    private MovementState state;

    private float moveProgress;

    private int fallDistance;

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
        if (state == MovementState.Moving)
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

        // commit voxel
        agent.SetCurrentVoxel(pendingTargetVoxel);

        moveProgress = 0f;
        state = MovementState.Idle;

        // AFTER ARRIVAL: resolve gravity
        ResolvePostMove();
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
            BeginFall();
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

        MoveToVoxel(forward);
    }

    // --------------------------------------------------
    // FALL START
    // --------------------------------------------------
    private void BeginFall()
    {
        fallDistance = 0;

        Vector3Int below = agent.CurrentVoxel + Vector3Int.down;
        MoveToVoxel(below);
    }

    // --------------------------------------------------
    // AFTER EACH MOVE (critical gravity logic)
    // --------------------------------------------------
    private void ResolvePostMove()
    {
        Vector3Int below = agent.CurrentVoxel + Vector3Int.down;

        if (!world.HasSupport(below))
        {
            fallDistance++;
            MoveToVoxel(below);
            return;
        }

        // landed
        if (fallDistance > 5)
            Die();

        fallDistance = 0;
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
    public void MoveToVoxel(Vector3Int targetVoxel)
    {
        if (state == MovementState.Moving)
            return;

        pendingTargetVoxel = targetVoxel;

        startWorldPos = transform.position;
        targetWorldPos = VoxelToWorld(targetVoxel);

        moveProgress = 0f;
        state = MovementState.Moving;
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