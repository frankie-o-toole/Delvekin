using UnityEngine;
using UnityEngine.InputSystem;

public class DwarfSelectionManager : MonoBehaviour
{
    private DwarfAgent selectedDwarf;

    private void Update()
    {
        UpdateDwarfHover();
        HandleSelection();
        HandleAbilityKeys();
    }

    private void UpdateDwarfHover()
    {
        InteractionState.IsHoveringDwarf = false;

        if (Mouse.current == null)
            return;

        Ray ray = Camera.main.ScreenPointToRay(
            Mouse.current.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit, 100f))
            return;

        if (hit.collider.GetComponent<DwarfAgent>() != null)
        {
            InteractionState.IsHoveringDwarf = true;
        }
    }

    private void HandleSelection()
    {
        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;


        Ray ray = Camera.main.ScreenPointToRay(
            Mouse.current.position.ReadValue());


        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            DwarfAgent dwarf =
                hit.collider.GetComponentInParent<DwarfAgent>();

            if (dwarf == null)
                return;


            if (selectedDwarf == dwarf)
            {
                selectedDwarf = null;
                Debug.Log("Dwarf deselected");
                return;
            }


            selectedDwarf = dwarf;

            Debug.Log(
                $"Selected dwarf {dwarf.name}");
        }
    }


    private void HandleAbilityKeys()
    {
        if (selectedDwarf == null)
            return;


        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            AssignDirectionAlter();
        }

        if (Keyboard.current.wKey.wasPressedThisFrame)
        {
            // future tunnel
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            // future ladder
        }
    }


    private void AssignDirectionAlter()
    {
        DwarfAbilityController controller =
            selectedDwarf.GetComponent<DwarfAbilityController>();

        controller.AssignAbility(
            new DirectionAlterAbility());


        Debug.Log("Assigned Direction Alter");

        selectedDwarf = null;
    }
}