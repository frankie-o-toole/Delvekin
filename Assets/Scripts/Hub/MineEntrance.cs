using System;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Delvekin/Hub/Mine Entrance")]
public sealed class MineEntrance : MonoBehaviour
{
    [SerializeField]
    private LevelDefinition level;

    [Tooltip(
        "Convenience input for graybox entrances with a Collider. " +
        "Disable when a dedicated UI/input presenter handles activation.")]
    [SerializeField]
    private bool activateOnMouseUp = true;

    public LevelDefinition Level => level;
    public string LevelId => level != null
        ? level.LevelId
        : string.Empty;
    public HubLevelState CurrentState { get; private set; }
    public bool IsLaunchable => CurrentState?.IsLaunchable == true;

    public event Action<MineEntrance, HubLevelState> StateChanged;
    public event Action<MineEntrance> ActivationRequested;

    public void Configure(LevelDefinition definition)
    {
        level = definition;
    }

    public void ApplyState(HubLevelState state)
    {
        CurrentState = state ?? HubLevelState.Invalid(level);
        StateChanged?.Invoke(this, CurrentState);
    }

    public bool RequestActivation()
    {
        if (!IsLaunchable)
        {
            return false;
        }

        ActivationRequested?.Invoke(this);
        return true;
    }

    private void OnMouseUpAsButton()
    {
        if (activateOnMouseUp)
        {
            RequestActivation();
        }
    }
}
