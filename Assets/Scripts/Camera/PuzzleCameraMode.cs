using UnityEngine;

public class PuzzleCameraMode : MonoBehaviour, ICameraMode
{
    [SerializeField]
    private Vector3 puzzlePosition = new(0, 20, 0);

    [SerializeField]
    private Vector3 lookAtPoint = Vector3.zero;

    public void Enter()
    {
    }

    public void Exit()
    {
    }

    public void HandleInput()
    {
    }

    public void UpdateCamera()
    {
    }

    public Vector3 GetPuzzlePosition()
    {
        return puzzlePosition;
    }

    public Quaternion GetPuzzleRotation()
    {
        return Quaternion.LookRotation(
            lookAtPoint - puzzlePosition,
            Vector3.up);
    }
}