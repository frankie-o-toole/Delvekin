using System;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Delvekin/Hub/Hub Campaign Controller")]
public sealed class HubCampaignController : MonoBehaviour
{
    [SerializeField]
    private HubCampaignDefinition campaignDefinition;

    [Tooltip(
        "Automatically registers MineEntrance components in the loaded hub.")]
    [SerializeField]
    private bool discoverEntrancesOnEnable = true;

    [Tooltip(
        "Useful while no level-information UI exists. When disabled, an " +
        "entrance only selects its level and UI can launch it explicitly.")]
    [SerializeField]
    private bool launchImmediatelyOnEntranceActivation;

    [SerializeField]
    private List<MineEntrance> entrances = new();

    private readonly Dictionary<string, HubLevelState> states =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, HubLevelPresentation> presentations =
        new(StringComparer.Ordinal);
    private CampaignProgressService campaign;
    private GameFlowService gameFlow;

    public HubCampaignDefinition Definition => campaignDefinition;
    public HubLevelState SelectedLevelState { get; private set; }
    public HubLevelPresentation SelectedLevelPresentation { get; private set; }
    public IReadOnlyDictionary<string, HubLevelState> States => states;
    public IReadOnlyDictionary<string, HubLevelPresentation> Presentations =>
        presentations;

    public event Action<HubLevelState> LevelStateChanged;
    public event Action<HubLevelState> LevelSelected;
    public event Action<HubLevelPresentation> LevelPresentationSelected;
    public event Action LevelSelectionCleared;
    public event Action<string> LaunchRejected;

    private void OnEnable()
    {
        if (discoverEntrancesOnEnable)
        {
            DiscoverEntrances();
        }

        BindServices(
            CampaignProgressService.Instance,
            GameFlowService.Instance);
        SubscribeEntrances();
        RefreshAll();
    }

    private void Start()
    {
        if (campaign == null || gameFlow == null)
        {
            BindServices(
                CampaignProgressService.Instance,
                GameFlowService.Instance);
            RefreshAll();
        }
    }

    private void OnDisable()
    {
        UnsubscribeEntrances();

        if (campaign != null)
        {
            campaign.CampaignChanged -= HandleCampaignChanged;
        }
    }

    public void Configure(
        HubCampaignDefinition definition,
        CampaignProgressService campaignService = null,
        GameFlowService flowService = null)
    {
        campaignDefinition = definition;
        BindServices(
            campaignService ?? CampaignProgressService.Instance,
            flowService ?? GameFlowService.Instance);
        RefreshAll();
    }

    public void RegisterEntrance(MineEntrance entrance)
    {
        if (entrance == null || entrances.Contains(entrance))
        {
            return;
        }

        entrances.Add(entrance);
        entrance.ActivationRequested += HandleEntranceActivation;
        ApplyStateToEntrance(entrance);
    }

    public void UnregisterEntrance(MineEntrance entrance)
    {
        if (entrance == null || !entrances.Remove(entrance))
        {
            return;
        }

        entrance.ActivationRequested -= HandleEntranceActivation;
    }

    public void RefreshAll()
    {
        states.Clear();
        presentations.Clear();

        if (campaignDefinition != null)
        {
            foreach (HubLevelNode node in campaignDefinition.Levels)
            {
                HubLevelState state =
                    HubLevelStateResolver.Resolve(node, campaign);

                if (!state.IsValid || states.ContainsKey(state.LevelId))
                {
                    continue;
                }

                states.Add(state.LevelId, state);
                presentations.Add(
                    state.LevelId,
                    HubLevelPresentationResolver.Resolve(
                        state,
                        node,
                        campaign));
                LevelStateChanged?.Invoke(state);
            }
        }

        foreach (MineEntrance entrance in entrances)
        {
            ApplyStateToEntrance(entrance);
        }

        if (SelectedLevelState != null &&
            states.TryGetValue(
                SelectedLevelState.LevelId,
                out HubLevelState refreshedSelection))
        {
            SelectedLevelState = refreshedSelection;
            presentations.TryGetValue(
                refreshedSelection.LevelId,
                out HubLevelPresentation refreshedPresentation);
            SelectedLevelPresentation = refreshedPresentation;
            LevelSelected?.Invoke(refreshedSelection);
            LevelPresentationSelected?.Invoke(refreshedPresentation);
        }
    }

    public bool TryGetState(
        string levelId,
        out HubLevelState state)
    {
        if (string.IsNullOrWhiteSpace(levelId))
        {
            state = null;
            return false;
        }

        return states.TryGetValue(levelId.Trim(), out state);
    }

    public bool TryGetPresentation(
        string levelId,
        out HubLevelPresentation presentation)
    {
        if (string.IsNullOrWhiteSpace(levelId))
        {
            presentation = null;
            return false;
        }

        return presentations.TryGetValue(
            levelId.Trim(),
            out presentation);
    }

    public bool SelectLevel(string levelId)
    {
        if (!TryGetState(levelId, out HubLevelState state) ||
            !state.IsValid)
        {
            LaunchRejected?.Invoke(levelId ?? string.Empty);
            return false;
        }

        SelectedLevelState = state;
        presentations.TryGetValue(
            state.LevelId,
            out HubLevelPresentation presentation);
        SelectedLevelPresentation = presentation;
        LevelSelected?.Invoke(state);
        LevelPresentationSelected?.Invoke(presentation);
        return true;
    }

    public void ClearSelection()
    {
        SelectedLevelState = null;
        SelectedLevelPresentation = null;
        LevelSelectionCleared?.Invoke();
    }

    public bool LaunchSelectedLevel()
    {
        if (SelectedLevelState == null ||
            !SelectedLevelState.IsLaunchable)
        {
            LaunchRejected?.Invoke(
                SelectedLevelState?.LevelId ?? string.Empty);
            return false;
        }

        gameFlow ??= GameFlowService.Instance;

        if (gameFlow == null ||
            !gameFlow.LaunchLevel(SelectedLevelState.Level))
        {
            LaunchRejected?.Invoke(SelectedLevelState.LevelId);
            return false;
        }

        return true;
    }

    public bool TryLaunchLevel(string levelId)
    {
        return SelectLevel(levelId) && LaunchSelectedLevel();
    }

    [ContextMenu("Development/Refresh Hub States")]
    private void RefreshFromContextMenu()
    {
        RefreshAll();
    }

    [ContextMenu("Development/Launch Selected Level")]
    private void LaunchSelectedFromContextMenu()
    {
        LaunchSelectedLevel();
    }

    private void BindServices(
        CampaignProgressService campaignService,
        GameFlowService flowService)
    {
        if (campaign != null)
        {
            campaign.CampaignChanged -= HandleCampaignChanged;
        }

        campaign = campaignService;
        gameFlow = flowService;

        if (campaign != null)
        {
            campaign.CampaignChanged -= HandleCampaignChanged;
            campaign.CampaignChanged += HandleCampaignChanged;
        }
    }

    private void DiscoverEntrances()
    {
        MineEntrance[] discovered =
            FindObjectsByType<MineEntrance>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        entrances.Clear();
        entrances.AddRange(discovered);
    }

    private void SubscribeEntrances()
    {
        foreach (MineEntrance entrance in entrances)
        {
            if (entrance == null)
            {
                continue;
            }

            entrance.ActivationRequested -= HandleEntranceActivation;
            entrance.ActivationRequested += HandleEntranceActivation;
        }
    }

    private void UnsubscribeEntrances()
    {
        foreach (MineEntrance entrance in entrances)
        {
            if (entrance != null)
            {
                entrance.ActivationRequested -= HandleEntranceActivation;
            }
        }
    }

    private void ApplyStateToEntrance(MineEntrance entrance)
    {
        if (entrance == null)
        {
            return;
        }

        HubLevelState state = null;

        if (!string.IsNullOrWhiteSpace(entrance.LevelId))
        {
            states.TryGetValue(entrance.LevelId.Trim(), out state);
        }

        entrance.ApplyState(
            state ?? HubLevelState.Invalid(entrance.Level));
    }

    private void HandleEntranceActivation(MineEntrance entrance)
    {
        if (entrance == null || !SelectLevel(entrance.LevelId))
        {
            return;
        }

        if (launchImmediatelyOnEntranceActivation)
        {
            LaunchSelectedLevel();
        }
    }

    private void HandleCampaignChanged()
    {
        RefreshAll();
    }
}
