using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Serialization;

public class DwarfJobBarUI : MonoBehaviour
{
    public enum JobSlotState
    {
        Locked,
        UnavailableInLevel,
        Exhausted,
        Available
    }

    [Serializable]
    private class JobButtonBinding
    {
        public DwarfJobType jobType;
        public string displayName;
        public Sprite icon;
        public Button button;
        public TMP_Text countLabel;

        [NonSerialized]
        public TMP_Text stockLabel;

        [NonSerialized]
        public Image iconImage;

        [NonSerialized]
        public UnityAction callback;
    }

    [SerializeField]
    private DwarfJobAssignmentManager assignmentManager;

    [SerializeField]
    private VoxelWorld voxelWorld;

    [Header("Fixed Job Bar")]
    [Tooltip("Optional art prefab used for future locked slots. " +
             "The first authored button is used until one is supplied.")]
    [SerializeField]
    private Button jobButtonPrefab;

    [SerializeField]
    private Transform jobButtonContainer;

    [Min(1)]
    [SerializeField]
    private int fixedSlotCount = 9;

    [Min(1f)]
    [SerializeField]
    private float jobSlotWidth = 90f;

    [Min(1f)]
    [SerializeField]
    private float jobSlotHeight = 64f;

    [Min(0f)]
    [SerializeField]
    private float jobSlotSpacing = 8f;

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
    private bool fixedSlotsInitialized;

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

        if (CampaignProgressService.Instance != null)
        {
            CampaignProgressService.Instance.CampaignChanged +=
                HandleCampaignChanged;
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

        if (CampaignProgressService.Instance != null)
        {
            CampaignProgressService.Instance.CampaignChanged -=
                HandleCampaignChanged;
        }

        UnbindButtons();
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
        EnsureFixedJobSlots();
        ConfigureFixedSlotLayout();
        BindButtons();
    }

    private void OnValidate()
    {
        fixedSlotCount = Mathf.Max(1, fixedSlotCount);
        jobSlotWidth = Mathf.Max(1f, jobSlotWidth);
        jobSlotHeight = Mathf.Max(1f, jobSlotHeight);
        jobSlotSpacing = Mathf.Max(0f, jobSlotSpacing);

        if (Application.isPlaying && isActiveAndEnabled)
        {
            RebuildJobButtons();
            RefreshAllButtons();
        }
    }

    private void EnsureFixedJobSlots()
    {
        if (fixedSlotsInitialized)
        {
            return;
        }

        Button template = jobButtonPrefab;

        foreach (JobButtonBinding definition in jobButtonDefinitions)
        {
            if (definition?.button == null)
            {
                continue;
            }

            template ??= definition.button;
        }

        if (template == null)
        {
            return;
        }

        Transform originalParent = null;

        foreach (JobButtonBinding definition in jobButtonDefinitions)
        {
            if (definition?.button != null)
            {
                originalParent = definition.button.transform.parent;
                break;
            }
        }

        bool needsDedicatedContainer =
            jobButtonContainer == null ||
            stopJobButton != null &&
            stopJobButton.transform.parent == jobButtonContainer;

        if (needsDedicatedContainer)
        {
            jobButtonContainer =
                CreateFixedSlotContainer(originalParent);
        }

        if (jobButtonContainer == null)
        {
            return;
        }

        runtimeJobButtons.Clear();

        foreach (JobButtonBinding definition in jobButtonDefinitions)
        {
            if (definition?.button == null)
            {
                continue;
            }

            definition.button.transform.SetParent(
                jobButtonContainer,
                false);
            definition.button.transform.localScale = Vector3.one;
            definition.button.gameObject.SetActive(true);
            definition.iconImage = EnsureIconImage(definition.button);
            definition.stockLabel = EnsureStockLabel(definition);
            runtimeJobButtons.Add(definition);
        }

        while (runtimeJobButtons.Count < fixedSlotCount)
        {
            Button placeholder = Instantiate(
                template,
                jobButtonContainer);
            placeholder.name =
                $"LockedJobSlot{runtimeJobButtons.Count + 1}";
            placeholder.transform.localScale = Vector3.one;
            placeholder.onClick.RemoveAllListeners();
            placeholder.gameObject.SetActive(true);

            JobButtonBinding binding = new()
            {
                jobType = DwarfJobType.None,
                displayName = "?",
                icon = null,
                button = placeholder,
                countLabel = FindPrimaryLabel(placeholder)
            };

            binding.iconImage = EnsureIconImage(placeholder);
            binding.stockLabel = EnsureStockLabel(binding);
            runtimeJobButtons.Add(binding);
        }

        fixedSlotsInitialized = true;
    }

    private Transform CreateFixedSlotContainer(
        Transform parent)
    {
        if (parent == null)
        {
            return null;
        }

        GameObject containerObject = new(
            "FixedJobSlots",
            typeof(RectTransform),
            typeof(GridLayoutGroup),
            typeof(LayoutElement));

        RectTransform containerRect =
            containerObject.GetComponent<RectTransform>();
        containerRect.SetParent(parent, false);
        containerRect.SetSiblingIndex(0);

        return containerRect;
    }

    private void ConfigureFixedSlotLayout()
    {
        if (jobButtonContainer == null)
        {
            return;
        }

        GridLayoutGroup grid =
            jobButtonContainer.GetComponent<GridLayoutGroup>();
        grid ??= jobButtonContainer.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(jobSlotWidth, jobSlotHeight);
        grid.spacing = new Vector2(jobSlotSpacing, 0f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.MiddleLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        grid.constraintCount = 1;

        int visibleSlotCount = Mathf.Max(
            fixedSlotCount,
            runtimeJobButtons.Count);
        float width = visibleSlotCount * jobSlotWidth +
                      Mathf.Max(0, visibleSlotCount - 1) * jobSlotSpacing;

        LayoutElement layout =
            jobButtonContainer.GetComponent<LayoutElement>();
        layout ??= jobButtonContainer.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.flexibleWidth = 0f;
        layout.minHeight = jobSlotHeight;
        layout.preferredHeight = jobSlotHeight;

        if (jobButtonContainer is RectTransform containerRect)
        {
            containerRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                width);
            containerRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                jobSlotHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }
    }

    private TMP_Text EnsureStockLabel(
        JobButtonBinding binding)
    {
        Transform existing = binding.button.transform.Find("StockCount");

        if (existing != null)
        {
            return existing.GetComponent<TMP_Text>();
        }

        GameObject stockObject = new(
            "StockCount",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));

        RectTransform stockRect =
            stockObject.GetComponent<RectTransform>();
        stockRect.SetParent(binding.button.transform, false);
        stockRect.anchorMin = new Vector2(1f, 0f);
        stockRect.anchorMax = new Vector2(1f, 0f);
        stockRect.pivot = new Vector2(1f, 0f);
        stockRect.anchoredPosition = new Vector2(-5f, 4f);
        stockRect.sizeDelta = new Vector2(36f, 24f);

        TMP_Text stockLabel = stockObject.GetComponent<TMP_Text>();
        stockLabel.alignment = TextAlignmentOptions.BottomRight;
        stockLabel.raycastTarget = false;
        stockLabel.fontSize = 18f;

        if (binding.countLabel != null)
        {
            stockLabel.font = binding.countLabel.font;
            stockLabel.color = binding.countLabel.color;
        }

        return stockLabel;
    }

    private static Image EnsureIconImage(
        Button button)
    {
        Transform existing = button.transform.Find("JobIcon");

        if (existing != null)
        {
            return existing.GetComponent<Image>();
        }

        GameObject iconObject = new(
            "JobIcon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        RectTransform iconRect =
            iconObject.GetComponent<RectTransform>();
        iconRect.SetParent(button.transform, false);
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(8f, 8f);
        iconRect.offsetMax = new Vector2(-8f, -8f);

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        iconImage.enabled = false;
        return iconImage;
    }

    private static TMP_Text FindPrimaryLabel(
        Button button)
    {
        foreach (TMP_Text label in
                 button.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.name != "StockCount")
            {
                return label;
            }
        }

        return null;
    }

    private void BindButtons()
    {
        foreach (JobButtonBinding binding in runtimeJobButtons)
        {
            if (binding.button == null ||
                binding.jobType == DwarfJobType.None ||
                assignmentManager == null)
            {
                continue;
            }

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

        bool unlocked =
            implemented &&
            CampaignProgressService.Instance != null &&
            CampaignProgressService.Instance.IsJobUnlocked(
                binding.jobType);

        bool offeredInLevel =
            unlocked &&
            IsOfferedInLevel(binding.jobType);

        JobSlotState state = ResolveJobSlotState(
            implemented,
            unlocked,
            offeredInLevel,
            count);

        bool available =
            assignmentManager != null &&
            assignmentManager.InteractionEnabled &&
            state == JobSlotState.Available;

        bool selected =
            assignmentManager != null &&
            (assignmentManager.SelectedJob ==
                binding.jobType ||
            binding.jobType == DwarfJobType.DirectionAlter &&
            assignmentManager.AreDirectionAltererOptionsOpen);

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
                state == JobSlotState.Locked
                    ? "?"
                    : binding.icon != null
                        ? string.Empty
                        : displayName;
        }

        if (binding.iconImage != null)
        {
            binding.iconImage.sprite = binding.icon;
            binding.iconImage.enabled =
                state != JobSlotState.Locked &&
                binding.icon != null;
            binding.iconImage.color =
                state == JobSlotState.Available
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.4f);
        }

        if (binding.stockLabel != null)
        {
            binding.stockLabel.text = state switch
            {
                JobSlotState.Available => count.ToString(),
                JobSlotState.Exhausted => "0",
                JobSlotState.UnavailableInLevel => "—",
                _ => string.Empty
            };
        }
    }

    private bool IsOfferedInLevel(
        DwarfJobType jobType)
    {
        foreach (EffectiveJobAvailability job in inventory.EffectiveJobs)
        {
            if (job.JobType == jobType)
            {
                return true;
            }
        }

        return false;
    }

    public static JobSlotState ResolveJobSlotState(
        bool implemented,
        bool unlocked,
        bool offeredInLevel,
        int count)
    {
        if (!implemented || !unlocked)
        {
            return JobSlotState.Locked;
        }

        if (!offeredInLevel)
        {
            return JobSlotState.UnavailableInLevel;
        }

        return count > 0
            ? JobSlotState.Available
            : JobSlotState.Exhausted;
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

    private void HandleCampaignChanged()
    {
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
