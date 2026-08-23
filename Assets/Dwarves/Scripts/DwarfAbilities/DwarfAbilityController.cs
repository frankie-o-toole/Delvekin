using UnityEngine;

public class DwarfAbilityController : MonoBehaviour
{
    private IDwarfAbility currentAbility;
    private bool pendingActivation;

    public bool HasAbility =>
        currentAbility != null;

    public bool ControlsMovement =>
        currentAbility != null &&
        currentAbility.ControlsMovement;

    public void AssignAbility(IDwarfAbility ability)
    {
        currentAbility?.Exit();

        currentAbility = ability;

        // Wait until movement reaches the next voxel center.
        pendingActivation = true;
    }

    public void ActivatePendingAbility()
    {
        if (!pendingActivation || currentAbility == null)
            return;

        pendingActivation = false;

        currentAbility.Enter(GetComponent<DwarfAgent>());
    }

    private void Update()
    {
        if (pendingActivation)
            return;

        currentAbility?.Tick();

        if (currentAbility != null &&
            currentAbility.IsComplete)
        {
            currentAbility.Exit();
            currentAbility = null;
        }
    }
}