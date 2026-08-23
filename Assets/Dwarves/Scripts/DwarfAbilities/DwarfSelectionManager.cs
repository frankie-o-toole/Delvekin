using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class DwarfSelectionManager : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField]
    private Camera cam;

    [SerializeField]
    private LayerMask dwarfLayerMask;

    [SerializeField]
    private float maximumRayDistance = 1000f;

    private DwarfAgent hoveredDwarf;
    private DwarfAgent selectedDwarf;

    private void Awake()
    {
        if (cam == null)
        {
            cam = Camera.main;
        }
    }

    private void Update()
    {
        ValidateCurrentSelection();
        UpdateDwarfHover();
        HandleSelection();
        HandleAbilityKeys();
    }

    private void OnDisable()
    {
        SetHoveredDwarf(null);
        SetSelectedDwarf(null);

        InteractionState.ClearHoveredDwarf();
    }

    private void ValidateCurrentSelection()
    {
        if (selectedDwarf != null &&
            !selectedDwarf.IsActive)
        {
            SetSelectedDwarf(null);
        }

        if (hoveredDwarf != null &&
            !hoveredDwarf.IsActive)
        {
            SetHoveredDwarf(null);
        }
    }

    private void UpdateDwarfHover()
    {
        if (Mouse.current == null ||
            cam == null ||
            IsPointerOverUI())
        {
            SetHoveredDwarf(null);
            return;
        }

        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        Ray ray =
            cam.ScreenPointToRay(
                mousePosition);

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                maximumRayDistance,
                dwarfLayerMask,
                QueryTriggerInteraction.Collide))
        {
            SetHoveredDwarf(null);
            return;
        }

        DwarfAgent dwarf =
            hit.collider.GetComponentInParent<DwarfAgent>();

        if (dwarf == null ||
            !dwarf.IsActive)
        {
            SetHoveredDwarf(null);
            return;
        }

        SetHoveredDwarf(dwarf);
    }

    private void HandleSelection()
    {
        if (Mouse.current == null ||
            !Mouse.current.leftButton.wasPressedThisFrame ||
            IsPointerOverUI())
        {
            return;
        }

        if (hoveredDwarf == null)
        {
            SetSelectedDwarf(null);
            return;
        }

        if (selectedDwarf == hoveredDwarf)
        {
            SetSelectedDwarf(null);

            Debug.Log(
                "Dwarf deselected");

            return;
        }

        SetSelectedDwarf(
            hoveredDwarf);

        Debug.Log(
            $"Selected dwarf {hoveredDwarf.name}");
    }

    private void SetHoveredDwarf(
        DwarfAgent dwarf)
    {
        if (hoveredDwarf == dwarf)
        {
            InteractionState.SetHoveredDwarf(
                dwarf);

            return;
        }

        DwarfHighlight previousHighlight =
            GetDwarfHighlight(
                hoveredDwarf);

        if (previousHighlight != null)
        {
            previousHighlight.SetHovered(false);
        }

        hoveredDwarf = dwarf;

        DwarfHighlight newHighlight =
            GetDwarfHighlight(
                hoveredDwarf);

        if (newHighlight != null)
        {
            newHighlight.SetHovered(true);
        }

        InteractionState.SetHoveredDwarf(
            hoveredDwarf);
    }

    private void SetSelectedDwarf(
        DwarfAgent dwarf)
    {
        if (selectedDwarf == dwarf)
        {
            return;
        }

        DwarfHighlight previousHighlight =
            GetDwarfHighlight(
                selectedDwarf);

        if (previousHighlight != null)
        {
            previousHighlight.SetSelected(false);
        }

        selectedDwarf = dwarf;

        DwarfHighlight newHighlight =
            GetDwarfHighlight(
                selectedDwarf);

        if (newHighlight != null)
        {
            newHighlight.SetSelected(true);
        }
    }

    /// <summary>
    /// Finds the highlight on the DwarfAgent object or anywhere
    /// beneath it in the prefab hierarchy.
    /// </summary>
    private DwarfHighlight GetDwarfHighlight(
        DwarfAgent dwarf)
    {
        if (dwarf == null)
        {
            return null;
        }

        DwarfHighlight highlight =
            dwarf.GetComponent<DwarfHighlight>();

        if (highlight == null)
        {
            highlight =
                dwarf.GetComponentInChildren<DwarfHighlight>(
                    includeInactive: true);
        }

        if (highlight == null)
        {
            Debug.LogWarning(
                $"No DwarfHighlight found on or beneath "
                + $"{dwarf.name}.",
                dwarf);
        }

        return highlight;
    }

    private void HandleAbilityKeys()
    {
        if (selectedDwarf == null ||
            Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            AssignDirectionAlter();
        }

        if (Keyboard.current.wKey.wasPressedThisFrame)
        {
            // Future tunneller.
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            // Future ladder or climber.
        }
    }

    private void AssignDirectionAlter()
    {
        if (selectedDwarf == null)
        {
            return;
        }

        DwarfAbilityController controller =
            selectedDwarf.GetComponent<DwarfAbilityController>();

        if (controller == null)
        {
            Debug.LogError(
                $"{selectedDwarf.name} has no "
                + "DwarfAbilityController.");

            SetSelectedDwarf(null);
            return;
        }

        controller.AssignAbility(
            new DirectionAlterAbility());

        Debug.Log(
            "Assigned Direction Alter");

        SetSelectedDwarf(null);
    }

    private bool IsPointerOverUI()
    {
        return
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject();
    }
}