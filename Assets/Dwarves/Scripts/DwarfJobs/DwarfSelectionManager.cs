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

    [Header("Jobs")]
    [SerializeField]
    private DwarfJobAssignmentManager assignmentManager;

    private DwarfAgent hoveredDwarf;
    private DwarfAgent visuallySelectedDwarf;

    private void Awake()
    {
        if (cam == null)
        {
            cam = Camera.main;
        }

        if (assignmentManager == null)
        {
            assignmentManager =
                FindFirstObjectByType<
                    DwarfJobAssignmentManager>();
        }
    }

    private void OnEnable()
    {
        if (assignmentManager == null)
            return;

        assignmentManager.SelectedDwarfChanged +=
            HandleSelectedDwarfChanged;

        assignmentManager.SelectedJobChanged +=
            HandleSelectedJobChanged;
    }

    private void OnDisable()
    {
        if (assignmentManager != null)
        {
            assignmentManager.SelectedDwarfChanged -=
                HandleSelectedDwarfChanged;

            assignmentManager.SelectedJobChanged -=
                HandleSelectedJobChanged;
        }

        SetHoveredDwarf(null);
        SetVisualSelection(null);

        InteractionState.ClearHoveredDwarf();
    }

    private void Update()
    {
        ValidateSelection();
        UpdateDwarfHover();
        RefreshHoveredTargetState();
        HandleSelection();
        HandleKeyboard();
    }

    private void ValidateSelection()
    {
        if (assignmentManager == null)
            return;

        DwarfAgent selected =
            assignmentManager.SelectedDwarf;

        if (selected != null &&
            !selected.IsActive)
        {
            assignmentManager.ClearSelectedDwarf();
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

        Ray ray =
            cam.ScreenPointToRay(
                Mouse.current.position.ReadValue());

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
        if (assignmentManager == null ||
            Mouse.current == null ||
            !Mouse.current.leftButton.wasPressedThisFrame ||
            IsPointerOverUI())
        {
            return;
        }

        if (hoveredDwarf == null)
        {
            assignmentManager.ClearSelectedDwarf();
            return;
        }

        assignmentManager.ToggleDwarf(
            hoveredDwarf);
    }

    private void HandleKeyboard()
    {
        if (assignmentManager == null ||
            Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.escapeKey
            .wasPressedThisFrame)
        {
            assignmentManager.ClearAllSelections();
        }

        // Temporary shortcut for the implemented job.
        if (Keyboard.current.qKey
            .wasPressedThisFrame)
        {
            assignmentManager.ToggleJob(
                DwarfJobType.DirectionAlter);
        }

        if (Keyboard.current.xKey
            .wasPressedThisFrame)
        {
            CancelSelectedDwarfJob();
        }
    }

    private void CancelSelectedDwarfJob()
    {
        DwarfAgent selected =
            assignmentManager.SelectedDwarf;

        if (selected == null)
        {
            return;
        }

        DwarfJobController controller =
            selected.GetComponent<DwarfJobController>();

        if (controller == null)
        {
            return;
        }

        if (controller.TryCancelActiveJob(
                out string failureReason))
        {
            Debug.Log(
                $"Cancelled active job on "
                + $"{selected.name}.");
        }
        else
        {
            Debug.LogWarning(
                $"Could not cancel job: "
                + failureReason);
        }

        assignmentManager.ClearSelectedDwarf();
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

        GetHighlight(hoveredDwarf)
            ?.SetHovered(false);

        GetHighlight(hoveredDwarf)
            ?.SetJobTargetState(false, false);

        hoveredDwarf = dwarf;

        GetHighlight(hoveredDwarf)
            ?.SetHovered(true);

        InteractionState.SetHoveredDwarf(
            hoveredDwarf);
    }

    private void RefreshHoveredTargetState()
    {
        DwarfHighlight highlight =
            GetHighlight(hoveredDwarf);

        if (highlight == null ||
            assignmentManager == null)
        {
            return;
        }

        if (!assignmentManager.HasSelectedJob)
        {
            highlight.SetJobTargetState(
                false,
                false);

            return;
        }

        bool valid =
            assignmentManager.CanAssignSelectedJob(
                hoveredDwarf,
                out _);

        highlight.SetJobTargetState(
            true,
            valid);
    }

    private void HandleSelectedDwarfChanged(
        DwarfAgent dwarf)
    {
        SetVisualSelection(dwarf);
        RefreshHoveredTargetState();
    }

    private void HandleSelectedJobChanged(
        DwarfJobType jobType)
    {
        RefreshHoveredTargetState();
    }

    private void SetVisualSelection(
        DwarfAgent dwarf)
    {
        GetHighlight(visuallySelectedDwarf)
            ?.SetSelected(false);

        visuallySelectedDwarf = dwarf;

        GetHighlight(visuallySelectedDwarf)
            ?.SetSelected(true);
    }

    private DwarfHighlight GetHighlight(
        DwarfAgent dwarf)
    {
        if (dwarf == null)
            return null;

        DwarfHighlight highlight =
            dwarf.GetComponent<DwarfHighlight>();

        if (highlight == null)
        {
            highlight =
                dwarf.GetComponentInChildren<DwarfHighlight>(
                    includeInactive: true);
        }

        return highlight;
    }

    private bool IsPointerOverUI()
    {
        return
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject();
    }
}