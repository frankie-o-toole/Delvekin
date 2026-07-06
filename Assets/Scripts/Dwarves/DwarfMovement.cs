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

    private Vector3 startWorldPos;
    private Vector3 targetWorldPos;

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

    private void UpdateMovement()
    {
        moveProgress += Time.deltaTime * moveSpeed;

        transform.position = Vector3.Lerp(
            startWorldPos,
            targetWorldPos,
            moveProgress);

        if (moveProgress >= 1f)
        {
            transform.position = targetWorldPos;

            agent.SetCurrentVoxel(agent.TargetVoxel);

            state = MovementState.Idle;
        }
    }

    private void DecideNextMove()
    {
        Vector3Int forward =
            agent.CurrentVoxel +
            DirectionUtility.ToVector(agent.Facing);

        Vector3Int support =
            forward + Vector3Int.down;

        if (world.GetVoxel(forward).Type != VoxelType.Air)
        {
            TurnAround();
            return;
        }

        if (world.GetVoxel(support).Type == VoxelType.Air)
        {
            // falling not implemented yet
            return;
        }

        MoveToVoxel(forward);
    }

    private void TurnAround()
    {
        agent.SetFacing(DirectionUtility.Opposite(agent.Facing));
    }

    public void MoveToVoxel(Vector3Int targetVoxel)
    {
        if (state == MovementState.Moving)
            return;

        agent.SetTargetVoxel(targetVoxel);

        startWorldPos = transform.position;
        targetWorldPos = VoxelToWorld(targetVoxel);

        moveProgress = 0f;
        state = MovementState.Moving;
    }

    private static Vector3 VoxelToWorld(Vector3Int voxel)
    {
        return new Vector3(
            voxel.x + 0.5f,
            voxel.y + 0.5f,
            voxel.z + 0.5f);
    }
}