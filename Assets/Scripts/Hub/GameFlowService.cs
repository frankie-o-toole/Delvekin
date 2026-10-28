using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
[AddComponentMenu("Delvekin/Game Flow Service")]
public sealed class GameFlowService : MonoBehaviour
{
    public const string DefaultGameplayScene = "Main";
    public const string DefaultHubScene = "DwarfIsland";

    [SerializeField]
    private string gameplaySceneName = DefaultGameplayScene;

    [SerializeField]
    private string hubSceneName = DefaultHubScene;

    private DwarfSpawner connectedSpawner;

    public static GameFlowService Instance { get; private set; }

    public LevelDefinition SelectedLevel { get; private set; }
    public bool IsTransitioning { get; private set; }
    public string GameplaySceneName => gameplaySceneName;
    public string HubSceneName => hubSceneName;

    public event Action<LevelDefinition> LevelLaunchRequested;
    public event Action<string> SceneTransitionStarted;
    public event Action<Scene> SceneTransitionCompleted;
    public event Action<string> SceneTransitionFailed;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject serviceObject = new("Game Flow Service");
        serviceObject.AddComponent<GameFlowService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        DisconnectSpawner();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Instance = null;
    }

    public bool LaunchLevel(LevelDefinition level)
    {
        if (level == null ||
            string.IsNullOrWhiteSpace(level.LevelId))
        {
            Debug.LogError(
                "Cannot launch a level without a stable LevelId.",
                this);
            return false;
        }

        if (IsTransitioning)
        {
            return false;
        }

        if (!CanLoadScene(gameplaySceneName))
        {
            return false;
        }

        SelectedLevel = level;
        LevelLaunchRequested?.Invoke(level);
        return BeginSceneTransition(gameplaySceneName);
    }

    public bool ReturnToHub()
    {
        if (IsTransitioning)
        {
            return false;
        }

        if (!CanLoadScene(hubSceneName))
        {
            return false;
        }

        return BeginSceneTransition(hubSceneName);
    }

    public void ConfigureSceneNames(
        string gameplayScene,
        string hubScene)
    {
        if (!string.IsNullOrWhiteSpace(gameplayScene))
        {
            gameplaySceneName = gameplayScene.Trim();
        }

        if (!string.IsNullOrWhiteSpace(hubScene))
        {
            hubSceneName = hubScene.Trim();
        }
    }

    private bool BeginSceneTransition(string sceneName)
    {
        Time.timeScale = 1f;
        IsTransitioning = true;
        DisconnectSpawner();
        SceneTransitionStarted?.Invoke(sceneName);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        if (operation == null)
        {
            IsTransitioning = false;
            string message = $"Failed to begin loading '{sceneName}'.";
            Debug.LogError(message, this);
            SceneTransitionFailed?.Invoke(message);
            return false;
        }

        return true;
    }

    private bool CanLoadScene(string sceneName)
    {
        if (!string.IsNullOrWhiteSpace(sceneName) &&
            Application.CanStreamedLevelBeLoaded(sceneName))
        {
            return true;
        }

        string message =
            $"Scene '{sceneName}' is not available in Build Settings.";
        Debug.LogError(message, this);
        SceneTransitionFailed?.Invoke(message);
        return false;
    }

    private void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        if (string.Equals(
                scene.name,
                gameplaySceneName,
                StringComparison.Ordinal))
        {
            ConfigureGameplayScene(scene);
        }

        IsTransitioning = false;
        SceneTransitionCompleted?.Invoke(scene);
    }

    private void ConfigureGameplayScene(Scene scene)
    {
        if (SelectedLevel != null)
        {
            LevelAuthoringRoot authoringRoot =
                FindFirstObjectByType<LevelAuthoringRoot>(
                    FindObjectsInactive.Include);

            if (authoringRoot == null ||
                !authoringRoot.ConfigureRuntimeLevel(SelectedLevel))
            {
                Debug.LogError(
                    $"Gameplay scene '{scene.name}' has no configured " +
                    "LevelAuthoringRoot for the selected level.",
                    this);
            }
        }

        connectedSpawner = FindFirstObjectByType<DwarfSpawner>(
            FindObjectsInactive.Include);

        if (connectedSpawner != null)
        {
            connectedSpawner.BackToCityRequested +=
                HandleBackToCityRequested;
        }
    }

    private void HandleBackToCityRequested()
    {
        ReturnToHub();
    }

    private void DisconnectSpawner()
    {
        if (connectedSpawner == null)
        {
            return;
        }

        connectedSpawner.BackToCityRequested -=
            HandleBackToCityRequested;
        connectedSpawner = null;
    }
}
