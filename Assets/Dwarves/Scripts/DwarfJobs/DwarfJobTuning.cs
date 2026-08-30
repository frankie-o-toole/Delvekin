using UnityEngine;

public class DwarfJobTuning : MonoBehaviour
{
    [Header("Tunneller")]
    [SerializeField]
    [Min(0.05f)]
    private float tunnelDepthCycleDuration = 0.65f;

    [Header("Digger")]
    [SerializeField]
    [Min(0.05f)]
    private float diggerDepthCycleDuration = 0.65f;

    [Header("Stair Builder")]
    [SerializeField]
    [Min(0.05f)]
    private float stairBuildInterval = 0.65f;

    [Header("Ladder Builder")]
    [SerializeField]
    [Min(0.05f)]
    private float ladderBuildInterval = 0.65f;

    public float TunnelDepthCycleDuration =>
        Mathf.Max(
            0.05f,
            tunnelDepthCycleDuration);

    public float DiggerDepthCycleDuration =>
        Mathf.Max(
            0.05f,
            diggerDepthCycleDuration);

    public float StairBuildInterval =>
        Mathf.Max(
            0.05f,
            stairBuildInterval);

    public float LadderBuildInterval =>
        Mathf.Max(
            0.05f,
            ladderBuildInterval);
}
