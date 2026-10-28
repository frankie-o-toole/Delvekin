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

    [SerializeField]
    private List<MineEntrance> entrances = new();

    private readonly Dictionary<string, HubLevelState> states =
        new(StringComparer.Ordinal);
    private CampaignProgressService campaign;
    private GameFlowService gameFlow;

    public HubCampaignDefinition Definition => campaignDefinition;
    public HubLevelState SelectedLevelState { get; private set; }
    public IReadOnlyDictionary<string, HubLevelState> States => states;

    public event Action<HubLevelState> LevelStateChanged;
    public event Action<HubLevelState> LevelSelected;
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

    public bool SelectLevel(string levelId)
    {
        if (!TryGetState(levelId, out HubLevelState state) ||
            !state.IsLaunchable)
        {
            LaunchRejected?.Invoke(levelId ?? string.Empty);
            return false;
        }

        SelectedLevelState = state;
        LevelSelected?.Invoke(state);
        return true;
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
        if (entrance != null)
        {
            SelectLevel(entrance.LevelId);
        }
    }

    private void HandleCampaignChanged()
    {
        RefreshAll();
    }
}
