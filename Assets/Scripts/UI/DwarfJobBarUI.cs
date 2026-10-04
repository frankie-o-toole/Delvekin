using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Serialization;

public class DwarfJobBarUI : MonoBehaviour
{
    [Serializable]
    private class JobButtonBinding
    {
        public DwarfJobType jobType;
        public string displayName;
        public Button button;
        public TMP_Text countLabel;

        [NonSerialized]
        public UnityAction callback;
    }

    [SerializeField]
    private DwarfJobAssignmentManager assignmentManager;

    [SerializeField]
    private VoxelWorld voxelWorld;

    [Header("Dynamic Job Buttons")]
    [Tooltip("Optional reusable prefab. Until an art prefab is supplied, " +
             "the first legacy button is used as the runtime template.")]
    [SerializeField]
    private Button jobButtonPrefab;

    [SerializeField]
    private Transform jobButtonContainer;

    [Tooltip("Total horizontal space the generated job buttons may share.")]
    [Min(1f)]
    [SerializeField]
    private float jobButtonWidthBudget = 900f;

    [Min(1f)]
    [SerializeField]
    private float minimumJobButtonWidth = 120f;

    [Min(1f)]
    [SerializeField]
    private float maximumJobButtonWidth = 225f;

    [Min(1f)]
    [SerializeField]
    private float jobButtonHeight = 45f;

    [Min(0f)]
    [SerializeField]
    private float jobButtonSpacing = 12f;

    [FormerlySerializedAs("jobButtons")]
    [SerializeField]
    private List<JobButtonBinding> jobButtonDefinitions =
        new();

    [SerializeField]
    private TMP_Text feedbackLabel;

    [Header("Stop Job")]
    [SerializeField]
    private Button stopJobButton;

    [SerializeField]
    private TMP_Text stopJobLabel;

    [SerializeField]
    private string stopJobDisplayName =
        "Stop Job";

    [Header("Direction Alterer Options")]
    [SerializeField]
    private GameObject directionAltererOptionsPanel;

    [SerializeField]
    private Button directionAltererLeftButton;

    [SerializeField]
    private Button directionAltererReverseButton;

    [SerializeField]
    private Button directionAltererRightButton;

    [Header("Colours")]
    [SerializeField]
    private Color normalColour =
        new Color(0.25f, 0.25f, 0.25f, 1f);

    [SerializeField]
    private Color selectedColour =
        new Color(0.9f, 0.65f, 0.15f, 1f);

    [SerializeField]
    private Color unavailableColour =
        new Color(0.15f, 0.15f, 0.15f, 0.65f);

    private DwarfJobInventory inventory;
    private UnityAction stopJobCallback;
    private UnityAction directionAltererLeftCallback;
    private UnityAction directionAltererReverseCallback;
    private UnityAction directionAltererRightCallback;
    private CanvasGroup canvasGroup;
    private readonly List<JobButtonBinding> runtimeJobButtons =
        new();

    private void Awake()
    {
        ResolveInventory();
        ResolveLoadingReferences();
    }

    private void Start()
    {
        ResolveInventory();
        SubscribeToInventory();
        RebuildJobButtons();
        RefreshAllButtons();
        RefreshLoadingVisibility();
    }

    private void ResolveInventory()
    {
        if (assignmentManager == null)
        {
            assignmentManager =
                FindFirstObjectByType<DwarfJobAssignmentManager>();
        }

        if (assignmentManager != null)
        {
            inventory = assignmentManager.Inventory;
        }

        if (inventory == null)
        {
            inventory =
                FindFirstObjectByType<DwarfJobInventory>();
        }
    }

    private void SubscribeToInventory()
    {
        if (inventory == null)
        {
            return;
        }

        inventory.CountChanged -= HandleCountChanged;
        inventory.CountChanged += HandleCountChanged;

        inventory.ConfigurationChanged -=
            HandleInventoryConfigurationChanged;
        inventory.ConfigurationChanged +=
            HandleInventoryConfigurationChanged;
    }

    private void OnEnable()
    {
        ResolveLoadingReferences();

        if (voxelWorld != null)
        {
            voxelWorld.LoadingProgressChanged +=
                HandleLoadingProgressChanged;
        }

        if (assignmentManager == null)
            return;

        assignmentManager.SelectedJobChanged +=
            HandleSelectedJobChanged;

        assignmentManager.AssignmentSucceeded +=
            HandleAssignmentSucceeded;

        assignmentManager.AssignmentFailed +=
            HandleAssignmentFailed;

        assignmentManager.StopJobSelectionChanged +=
            HandleStopJobSelectionChanged;

        assignmentManager.JobStopped +=
            HandleJobStopped;

        assignmentManager.DirectionAltererSelectionChanged +=
            HandleDirectionAltererSelectionChanged;

        assignmentManager.InteractionEnabledChanged +=
            HandleInteractionEnabledChanged;

        if (inventory != null)
        {
            SubscribeToInventory();
        }

        RebuildJobButtons();
        BindStopJobButton();
        BindDirectionAltererOptionButtons();
        RefreshAllButtons();
    }

    private void OnDisable()
    {
        if (voxelWorld != null)
        {
            voxelWorld.LoadingProgressChanged -=
                HandleLoadingProgressChanged;
        }

        if (assignmentManager != null)
        {
            assignmentManager.SelectedJobChanged -=
                HandleSelectedJobChanged;

            assignmentManager.AssignmentSucceeded -=
                HandleAssignmentSucceeded;

            assignmentManager.AssignmentFailed -=
                HandleAssignmentFailed;

            assignmentManager.StopJobSelectionChanged -=
                HandleStopJobSelectionChanged;

            assignmentManager.JobStopped -=
                HandleJobStopped;

            assignmentManager.DirectionAltererSelectionChanged -=
                HandleDirectionAltererSelectionChanged;

            assignmentManager.InteractionEnabledChanged -=
                HandleInteractionEnabledChanged;
        }

        if (inventory != null)
        {
            inventory.CountChanged -=
                HandleCountChanged;

            inventory.ConfigurationChanged -=
                HandleInventoryConfigurationChanged;
        }

        UnbindButtons();
        ClearRuntimeJobButtons();
        UnbindStopJobButton();
        UnbindDirectionAltererOptionButtons();
    }

    private void ResolveLoadingReferences()
    {
        if (voxelWorld == null)
        {
            voxelWorld = FindFirstObjectByType<VoxelWorld>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void HandleLoadingProgressChanged(
        LevelLoadingProgress progress)
    {
        RefreshLoadingVisibility();
    }

    private void RefreshLoadingVisibility()
    {
        if (canvasGroup == null)
        {
            return;
        }

        bool visible = voxelWorld == null || voxelWorld.IsLevelReady;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    public void RebuildJobButtons()
    {
        UnbindButtons();
        ClearRuntimeJobButtons();

        foreach (JobButtonBinding definition in jobButtonDefinitions)
        {
            if (definition?.button != null)
            {
                definition.button.gameObject.SetActive(false);
            }
        }

        Button template = ResolveJobButtonTemplate();

        if (inventory == null || template == null)
        {
            return;
        }

        Transform container = jobButtonContainer != null
            ? jobButtonContainer
            : template.transform.parent;

        if (container == null)
        {
            return;
        }

        PrepareExistingLayoutChildren(container);

        HorizontalLayoutGroup layout =
            container.GetComponent<HorizontalLayoutGroup>();

        if (layout != null)
        {
            layout.enabled = true;
            layout.spacing = jobButtonSpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        int siblingIndex = stopJobButton != null &&
                           stopJobButton.transform.parent == container
            ? stopJobButton.transform.GetSiblingIndex()
            : container.childCount;

        int jobCount = inventory.EffectiveJobs.Count;
        float buttonWidth = CalculateJobButtonWidth(
            jobCount,
            jobButtonWidthBudget,
            minimumJobButtonWidth,
            maximumJobButtonWidth);

        foreach (EffectiveJobAvailability job in inventory.EffectiveJobs)
        {
            Button button = Instantiate(template, container);
            button.name = $"{job.JobType}Button";
            button.transform.localScale = Vector3.one;

            if (button.transform is RectTransform buttonRect)
            {
                buttonRect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    buttonWidth);
                buttonRect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    jobButtonHeight);
            }

            ConfigureLayoutElement(
                button.gameObject,
                buttonWidth,
                jobButtonHeight);

            button.transform.SetSiblingIndex(siblingIndex++);
            button.gameObject.SetActive(true);

            JobButtonBinding definition =
                FindButtonDefinition(job.JobType);

            runtimeJobButtons.Add(new JobButtonBinding
            {
                jobType = job.JobType,
                displayName = definition?.displayName,
                button = button,
                countLabel = button.GetComponentInChildren<TMP_Text>(true)
            });
        }

        BindButtons();

        if (container is RectTransform containerRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }
    }

    private static void PrepareExistingLayoutChildren(
        Transform container)
    {
        for (int index = 0; index < container.childCount; index++)
        {
            Transform child = container.GetChild(index);

            if (!child.gameObject.activeSelf ||
                child is not RectTransform childRect)
            {
                continue;
            }

            float width = childRect.rect.width *
                          Mathf.Abs(childRect.localScale.x);
            float height = childRect.rect.height *
                           Mathf.Abs(childRect.localScale.y);

            childRect.localScale = Vector3.one;

            ConfigureLayoutElement(
                child.gameObject,
                Mathf.Max(1f, width),
                Mathf.Max(1f, height));
        }
    }

    private static void ConfigureLayoutElement(
        GameObject target,
        float width,
        float height)
    {
        LayoutElement layoutElement =
            target.GetComponent<LayoutElement>();

        if (layoutElement == null)
        {
            layoutElement = target.AddComponent<LayoutElement>();
        }

        layoutElement.ignoreLayout = false;
        layoutElement.minWidth = width;
        layoutElement.preferredWidth = width;
        layoutElement.flexibleWidth = 0f;
        layoutElement.minHeight = height;
        layoutElement.preferredHeight = height;
        layoutElement.flexibleHeight = 0f;
    }

    public static float CalculateJobButtonWidth(
        int jobCount,
        float widthBudget,
        float minimumWidth,
        float maximumWidth)
    {
        float safeMinimum = Mathf.Max(1f, minimumWidth);
        float safeMaximum = Mathf.Max(safeMinimum, maximumWidth);
        float safeBudget = Mathf.Max(safeMinimum, widthBudget);

        return Mathf.Clamp(
            safeBudget / Mathf.Max(1, jobCount),
            safeMinimum,
            safeMaximum);
    }

    private Button ResolveJobButtonTemplate()
    {
        if (jobButtonPrefab != null)
        {
            return jobButtonPrefab;
        }

        foreach (JobButtonBinding definition in jobButtonDefinitions)
        {
            if (definition?.button != null)
            {
                return definition.button;
            }
        }

        return null;
    }

    private JobButtonBinding FindButtonDefinition(
        DwarfJobType jobType)
    {
        return jobButtonDefinitions.Find(
            definition => definition != null &&
                          definition.jobType == jobType);
    }

    private void ClearRuntimeJobButtons()
    {
        foreach (JobButtonBinding binding in runtimeJobButtons)
        {
            if (binding?.button == null)
            {
                continue;
            }

            binding.button.gameObject.SetActive(false);

            if (Application.isPlaying)
            {
                Destroy(binding.button.gameObject);
            }
            else
            {
                DestroyImmediate(binding.button.gameObject);
            }
        }

        runtimeJobButtons.Clear();
    }

    private void OnValidate()
    {
        minimumJobButtonWidth =
            Mathf.Max(1f, minimumJobButtonWidth);
        maximumJobButtonWidth =
            Mathf.Max(minimumJobButtonWidth, maximumJobButtonWidth);
        jobButtonWidthBudget =
            Mathf.Max(minimumJobButtonWidth, jobButtonWidthBudget);
        jobButtonHeight =
            Mathf.Max(1f, jobButtonHeight);
        jobButtonSpacing =
            Mathf.Max(0f, jobButtonSpacing);

        if (Application.isPlaying && isActiveAndEnabled)
        {
            RebuildJobButtons();
            RefreshAllButtons();
        }
    }

    private void BindButtons()
    {
        foreach (JobButtonBinding binding in runtimeJobButtons)
        {
            if (binding.button == null)
                continue;

            DwarfJobType capturedType =
                binding.jobType;

            binding.callback =
                () => assignmentManager.ToggleJob(
                    capturedType);

            binding.button.onClick.AddListener(
                binding.callback);
        }
    }

    private void UnbindButtons()
    {
        foreach (JobButtonBinding binding in runtimeJobButtons)
        {
            if (binding.button == null ||
                binding.callback == null)
            {
                continue;
            }

            binding.button.onClick.RemoveListener(
                binding.callback);

            binding.callback = null;
        }
    }

    private void BindStopJobButton()
    {
        if (stopJobButton == null ||
            assignmentManager == null)
        {
            return;
        }

        stopJobCallback =
            assignmentManager.ToggleStopJob;

        stopJobButton.onClick.AddListener(
            stopJobCallback);
    }

    private void UnbindStopJobButton()
    {
        if (stopJobButton == null ||
            stopJobCallback == null)
        {
            return;
        }

        stopJobButton.onClick.RemoveListener(
            stopJobCallback);

        stopJobCallback = null;
    }

    private void BindDirectionAltererOptionButtons()
    {
        if (assignmentManager == null)
        {
            return;
        }

        directionAltererLeftCallback =
            () => assignmentManager.SelectDirectionAltererTurn(
                DirectionAltererTurn.Left);

        directionAltererReverseCallback =
            () => assignmentManager.SelectDirectionAltererTurn(
                DirectionAltererTurn.Reverse);

        directionAltererRightCallback =
            () => assignmentManager.SelectDirectionAltererTurn(
                DirectionAltererTurn.Right);

        directionAltererLeftButton?.onClick.AddListener(
            directionAltererLeftCallback);

        directionAltererReverseButton?.onClick.AddListener(
            directionAltererReverseCallback);

        directionAltererRightButton?.onClick.AddListener(
            directionAltererRightCallback);

        BindDirectionPreview(
            directionAltererLeftButton,
            DirectionAltererTurn.Left);

        BindDirectionPreview(
            directionAltererReverseButton,
            DirectionAltererTurn.Reverse);

        BindDirectionPreview(
            directionAltererRightButton,
            DirectionAltererTurn.Right);
    }

    private void UnbindDirectionAltererOptionButtons()
    {
        if (directionAltererLeftButton != null &&
            directionAltererLeftCallback != null)
        {
            directionAltererLeftButton.onClick.RemoveListener(
                directionAltererLeftCallback);
        }

        if (directionAltererReverseButton != null &&
            directionAltererReverseCallback != null)
        {
            directionAltererReverseButton.onClick.RemoveListener(
                directionAltererReverseCallback);
        }

        if (directionAltererRightButton != null &&
            directionAltererRightCallback != null)
        {
            directionAltererRightButton.onClick.RemoveListener(
                directionAltererRightCallback);
        }

        directionAltererLeftCallback = null;
        directionAltererReverseCallback = null;
        directionAltererRightCallback = null;

        UnbindDirectionPreview(directionAltererLeftButton);
        UnbindDirectionPreview(directionAltererReverseButton);
        UnbindDirectionPreview(directionAltererRightButton);
    }

    private void BindDirectionPreview(
        Button button,
        DirectionAltererTurn turn)
    {
        if (button == null ||
            assignmentManager == null)
        {
            return;
        }

        DirectionAltererOptionHover hover =
            button.GetComponent<DirectionAltererOptionHover>();

        if (hover == null)
        {
            hover =
                button.gameObject.AddComponent<
                    DirectionAltererOptionHover>();
        }

        hover.Initialize(
            assignmentManager,
            turn);
    }

    private static void UnbindDirectionPreview(
        Button button)
    {
        button
            ?.GetComponent<DirectionAltererOptionHover>()
            ?.Clear();
    }

    private void RefreshAllButtons()
    {
        foreach (JobButtonBinding binding in runtimeJobButtons)
        {
            RefreshButton(binding);
        }

        RefreshStopJobButton();
        RefreshDirectionAltererOptions();
    }

    private void RefreshStopJobButton()
    {
        if (stopJobButton == null)
        {
            return;
        }

        stopJobButton.interactable =
            assignmentManager != null &&
            assignmentManager.InteractionEnabled;

        if (stopJobButton.targetGraphic != null)
        {
            stopJobButton.targetGraphic.color =
                assignmentManager != null &&
                assignmentManager.IsStopJobSelected
                    ? selectedColour
                    : normalColour;
        }

        if (stopJobLabel != null)
        {
            stopJobLabel.text =
                stopJobDisplayName;
        }
    }

    private void RefreshDirectionAltererOptions()
    {
        bool optionsOpen =
            assignmentManager != null &&
            assignmentManager.AreDirectionAltererOptionsOpen;

        if (directionAltererOptionsPanel != null)
        {
            directionAltererOptionsPanel.SetActive(
                optionsOpen);
        }

        DirectionAltererTurn? selectedTurn =
            assignmentManager?.SelectedDirectionAltererTurn;

        SetDirectionOptionColour(
            directionAltererLeftButton,
            selectedTurn == DirectionAltererTurn.Left);

        SetDirectionOptionColour(
            directionAltererReverseButton,
            selectedTurn == DirectionAltererTurn.Reverse);

        SetDirectionOptionColour(
            directionAltererRightButton,
            selectedTurn == DirectionAltererTurn.Right);
    }

    private void SetDirectionOptionColour(
        Button button,
        bool selected)
    {
        if (button?.targetGraphic == null)
        {
            return;
        }

        button.targetGraphic.color =
            selected
                ? selectedColour
                : normalColour;
    }

    private void RefreshButton(
        JobButtonBinding binding)
    {
        if (binding.button == null ||
            inventory == null)
        {
            return;
        }

        int count =
            inventory.GetCount(
                binding.jobType);

        bool implemented =
            DwarfJobFactory.IsImplemented(
                binding.jobType);

        bool available =
            assignmentManager != null &&
            assignmentManager.InteractionEnabled &&
            implemented &&
            count > 0;

        bool selected =
            assignmentManager.SelectedJob ==
                binding.jobType ||
            binding.jobType == DwarfJobType.DirectionAlter &&
            assignmentManager.AreDirectionAltererOptionsOpen;

        binding.button.interactable =
            available;

        if (binding.button.targetGraphic != null)
        {
            binding.button.targetGraphic.color =
                !available
                    ? unavailableColour
                    : selected
                        ? selectedColour
                        : normalColour;
        }

        if (binding.countLabel != null)
        {
            string displayName =
                string.IsNullOrWhiteSpace(
                    binding.displayName)
                    ? binding.jobType.ToString()
                    : binding.displayName;

            binding.countLabel.text =
                $"{displayName} ({count})";
        }
    }

    private void HandleSelectedJobChanged(
        DwarfJobType jobType)
    {
        RefreshAllButtons();
    }

    private void HandleCountChanged(
        DwarfJobType jobType,
        int count)
    {
        RefreshAllButtons();
    }

    private void HandleInventoryConfigurationChanged()
    {
        RebuildJobButtons();
        RefreshAllButtons();
    }

    private void HandleAssignmentSucceeded(
        DwarfAgent dwarf,
        DwarfJobType jobType)
    {
        if (feedbackLabel != null)
        {
            feedbackLabel.text =
                $"Assigned {jobType} to {dwarf.name}";
        }
    }

    private void HandleAssignmentFailed(
        string failureReason)
    {
        if (feedbackLabel != null)
        {
            feedbackLabel.text =
                failureReason;
        }
    }

    private void HandleStopJobSelectionChanged(
        bool selected)
    {
        RefreshAllButtons();
    }

    private void HandleJobStopped(
        DwarfAgent dwarf,
        DwarfJobType jobType,
        bool dwarfRecalled)
    {
        if (feedbackLabel != null)
        {
            feedbackLabel.text =
                dwarfRecalled
                    ? $"Recalled {dwarf.name} after stopping {jobType}"
                    : $"Stopped {jobType} on {dwarf.name}";
        }
    }

    private void HandleDirectionAltererSelectionChanged(
        bool optionsOpen,
        DirectionAltererTurn? selectedTurn)
    {
        RefreshAllButtons();
    }

    private void HandleInteractionEnabledChanged(bool enabled)
    {
        RefreshAllButtons();
    }
}
