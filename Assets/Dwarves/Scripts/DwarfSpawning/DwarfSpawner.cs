using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class DwarfSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private DwarfPool pool;

    [SerializeField]
    private VoxelWorld world;

    [Header("Spawn Settings")]
    [SerializeField]
    private float spawnInterval = 2f;

    [SerializeField]
    private float blockedRetryInterval = 0.25f;

    [SerializeField]
    private int maxDwarves = 20;

    [SerializeField]
    [Min(1)]
    private int requiredRescues = 1;

    [SerializeField]
    private PuzzleSide initialFacing =
        PuzzleSide.North;

    private bool simulationStarted;
    private int spawned;
    private int rescued;
    private int died;
    private int recalled;
    private int nextSpawnPointIndex;
    private bool spawnFinished;
    private bool simulationResolved;
    private bool retryConfirmationOpen;
    private bool endLevelConfirmationOpen;
    private LevelSimulationState stateBeforeRetryConfirmation;
    private RescueGoalProgress rescueGoalProgress;
    private readonly LevelSimulationClock simulationClock = new();
    private LevelAttemptResult result;

    public LevelSimulationState SimulationState { get; private set; } =
        LevelSimulationState.Preparation;

    public LevelOutcome Outcome { get; private set; } =
        LevelOutcome.Undecided;

    public LevelSimulationSpeed SimulationSpeed { get; private set; } =
        LevelSimulationSpeed.Normal;

    public event System.Action<LevelSimulationState>
        SimulationStateChanged;

    public event System.Action<RescueGoalProgress>
        RescueGoalProgressChanged;

    public event System.Action<LevelSimulationSpeed>
        SimulationSpeedChanged;

    public int TotalDwarves => maxDwarves;
    public int RequiredRescues => GetRequiredRescues();
    public RescueGoalProgress RescueGoalProgress =>
        rescueGoalProgress;
    public bool IsRescueGoalImpossible =>
        rescueGoalProgress.IsImpossible;
    public bool IsRescueTargetReached =>
        rescueGoalProgress.TargetReached;
    public double SimulationElapsedSeconds =>
        simulationClock.ElapsedSeconds;
    public TimeSpan SimulationElapsed =>
        simulationClock.Elapsed;
    public LevelAttemptResult Result => result;

    public event System.Action BackToCityRequested;

    private void OnEnable()
    {
        if (pool != null)
        {
            pool.DwarfReleased +=
                HandleDwarfReleased;
        }
    }

    private void OnDisable()
    {
        if (pool != null)
        {
            pool.DwarfReleased -=
                HandleDwarfReleased;
        }
    }

    private void Update()
    {
        simulationClock.Advance(
            Time.deltaTime,
            SimulationState);
    }

    public void StartSimulation()
    {
        if (SimulationState != LevelSimulationState.Preparation)
        {
            return;
        }

        if (!ValidateReferences())
        {
            return;
        }

        world.ScanSpawnPoints();

        if (world.GetSpawnPoints().Count == 0)
        {
            Debug.LogError(
                "Cannot start dwarf simulation: "
                + "no SpawnPoint was found in the level.");

            return;
        }

        if (world.GetExitPoints().Count == 0)
        {
            Debug.LogError(
                "Cannot start dwarf simulation: "
                + "no ExitPoint was found in the level.");

            return;
        }

        WarnAboutUnsafeSpawnPoints();

        if (!world.CaptureAttemptStartSnapshot())
        {
            return;
        }

        spawned = 0;
        rescued = 0;
        died = 0;
        recalled = 0;
        nextSpawnPointIndex = 0;
        spawnFinished = false;
        simulationResolved = false;
        simulationStarted = true;
        Outcome = LevelOutcome.Undecided;
        simulationClock.Reset();
        result = null;

        ApplySimulationSpeed();

        RefreshRescueGoalProgress();

        SetSimulationState(LevelSimulationState.Running);

        world.StartFluidSimulation();

        StartCoroutine(SpawnLoop());
    }

    public void ResetSimulation()
    {
        StopAllCoroutines();

        Time.timeScale = 1f;
        retryConfirmationOpen = false;
        endLevelConfirmationOpen = false;

        // Disable result accounting before recalling active dwarves.
        simulationStarted = false;
        simulationResolved = false;
        spawnFinished = false;
        Outcome = LevelOutcome.Undecided;
        simulationClock.Reset();
        result = null;
        SetSimulationSpeed(LevelSimulationSpeed.Normal);

        if (pool != null)
        {
            List<DwarfAgent> activeDwarves =
                new(pool.ActiveDwarves);

            foreach (DwarfAgent dwarf in activeDwarves)
            {
                pool.Release(
                    dwarf,
                    DwarfReleaseReason.Recalled);
            }
        }

        spawned = 0;
        rescued = 0;
        died = 0;
        recalled = 0;
        nextSpawnPointIndex = 0;

        RefreshRescueGoalProgress();

        DwarfJobAssignmentManager assignmentManager =
            FindFirstObjectByType<DwarfJobAssignmentManager>();

        assignmentManager?.ClearAllSelections();

        DwarfJobInventory inventory =
            FindFirstObjectByType<DwarfJobInventory>();

        inventory?.ResetToStartingStock();

        SetSimulationState(LevelSimulationState.Preparation);
    }

    public void ConfigureLevel(
        LevelDefinition.RuntimeSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        maxDwarves = Mathf.Max(1, snapshot.TotalDwarves);
        requiredRescues = Mathf.Clamp(
            snapshot.RequiredRescues,
            1,
            maxDwarves);

        RefreshRescueGoalProgress();

        DwarfJobInventory inventory =
            FindFirstObjectByType<DwarfJobInventory>();

        inventory?.ConfigureForLevel(snapshot.JobRules);

        SetSimulationState(LevelSimulationState.Preparation);
    }

    public void WriteRuntimeConfigurationTo(
        LevelSaveData data,
        bool includeSceneJobRules)
    {
        if (data == null)
        {
            return;
        }

        data.totalDwarves = maxDwarves;
        data.requiredRescues = GetRequiredRescues();

        if (!includeSceneJobRules)
        {
            return;
        }

        DwarfJobInventory inventory =
            FindFirstObjectByType<DwarfJobInventory>();

        data.jobRules = inventory?.CreateStartingRules() ??
            new List<LevelJobRule>();
    }

    public void PauseSimulation()
    {
        if (SimulationState != LevelSimulationState.Running)
        {
            return;
        }

        Time.timeScale = 0f;
        SetSimulationState(LevelSimulationState.Paused);
    }

    public void ResumeSimulation()
    {
        if (SimulationState != LevelSimulationState.Paused)
        {
            return;
        }

        ApplySimulationSpeed();
        SetSimulationState(LevelSimulationState.Running);
    }

    public void SetSimulationSpeed(LevelSimulationSpeed speed)
    {
        if (!LevelSimulationSpeedUtility.IsSupported(speed))
        {
            return;
        }

        bool changed = SimulationSpeed != speed;
        SimulationSpeed = speed;

        if (SimulationState == LevelSimulationState.Running)
        {
            ApplySimulationSpeed();
        }

        if (changed)
        {
            SimulationSpeedChanged?.Invoke(SimulationSpeed);
        }
    }

    public void RequestRetry()
    {
        if (retryConfirmationOpen ||
            endLevelConfirmationOpen ||
            (SimulationState != LevelSimulationState.Running &&
             SimulationState != LevelSimulationState.Completed))
        {
            return;
        }

        stateBeforeRetryConfirmation = SimulationState;

        if (SimulationState == LevelSimulationState.Running)
        {
            PauseSimulation();
        }

        retryConfirmationOpen = true;
    }

    public void CancelRetry()
    {
        if (!retryConfirmationOpen)
        {
            return;
        }

        retryConfirmationOpen = false;

        if (stateBeforeRetryConfirmation ==
            LevelSimulationState.Running)
        {
            ResumeSimulation();
        }
    }

    public void ConfirmRetry()
    {
        if (!retryConfirmationOpen)
        {
            return;
        }

        retryConfirmationOpen = false;

        if (!world.RetryAttempt())
        {
            ResumeSimulation();
        }
    }

    public void RequestEndLevel()
    {
        if (endLevelConfirmationOpen ||
            retryConfirmationOpen ||
            SimulationState != LevelSimulationState.Running ||
            !IsRescueTargetReached)
        {
            return;
        }

        PauseSimulation();
        endLevelConfirmationOpen = true;
    }

    public void CancelEndLevel()
    {
        if (!endLevelConfirmationOpen)
        {
            return;
        }

        endLevelConfirmationOpen = false;
        ResumeSimulation();
    }

    public void ConfirmEndLevel()
    {
        if (!endLevelConfirmationOpen ||
            !IsRescueTargetReached)
        {
            return;
        }

        endLevelConfirmationOpen = false;
        CompleteSimulation(LevelOutcome.Success);
    }

    public void RequestBackToCity()
    {
        if (SimulationState != LevelSimulationState.Completed ||
            result == null)
        {
            return;
        }

        if (BackToCityRequested == null)
        {
            Debug.LogWarning(
                "Back to Dwarf City is not connected yet. "
                + "The result remains pending on this screen.",
                this);
            return;
        }

        BackToCityRequested.Invoke();
    }

    private IEnumerator SpawnLoop()
    {
        while (spawned < maxDwarves)
        {
            if (TrySpawnDwarf())
            {
                spawned++;
                RefreshRescueGoalProgress();

                yield return new WaitForSeconds(
                    spawnInterval);
            }
            else
            {
                // Every spawnpoint is currently invalid or occupied.
                // Wait without consuming one of the requested dwarves.
                yield return new WaitForSeconds(
                    blockedRetryInterval);
            }
        }


        spawnFinished = true;
        RefreshRescueGoalProgress();
        TryResolveSimulation();
    }

    private void HandleDwarfReleased(
        DwarfAgent dwarf,
        DwarfReleaseReason reason)
    {
        if (!simulationStarted ||
            simulationResolved)
        {
            return;
        }

        if (reason == DwarfReleaseReason.Rescued ||
            reason == DwarfReleaseReason.Mined)
        {
            // Step 7B keeps the existing rescue presentation operational.
            // Step 7C replaces this compatibility count with mined-resource
            // terminology and authored resource goals.
            rescued++;
        }
        else if (reason == DwarfReleaseReason.Died)
        {
            died++;
        }
        else
        {
            recalled++;
        }

        RefreshRescueGoalProgress();
        TryResolveSimulation();
    }

    private void TryResolveSimulation()
    {
        if (!spawnFinished ||
            rescued + died + recalled < spawned)
        {
            return;
        }

        CompleteSimulation(
            IsRescueTargetReached
                ? LevelOutcome.Success
                : LevelOutcome.Failure);
    }

    private bool TrySpawnDwarf()
    {
        var spawnPoints =
            world.GetSpawnPoints();

        if (spawnPoints.Count == 0)
        {
            return false;
        }

        for (int offset = 0;
             offset < spawnPoints.Count;
             offset++)
        {
            int index =
                (nextSpawnPointIndex + offset)
                % spawnPoints.Count;

            Vector3Int spawnVoxel =
                spawnPoints[index];

            if (!DwarfSpawnValidator.CanSpawn(
                    world,
                    spawnVoxel,
                    out string failureReason))
            {
                continue;
            }

            DwarfAgent dwarf =
                pool.Get();

            PuzzleSide spawnFacing =
                world.GetSpawnFacing(
                    spawnVoxel,
                    initialFacing);

            dwarf.Activate(
                spawnVoxel,
                spawnFacing);

            nextSpawnPointIndex =
                (index + 1)
                % spawnPoints.Count;

            return true;
        }

        return false;
    }

    private void WarnAboutUnsafeSpawnPoints()
    {
        int fatalDistance =
            pool.FatalFallDistance;

        foreach (Vector3Int spawnPoint in
                 world.GetSpawnPoints())
        {
            if (!DwarfSpawnValidator.TryGetLandingDistance(
                    world,
                    spawnPoint,
                    out int fallDistance))
            {
                Debug.LogWarning(
                    $"SpawnPoint at {spawnPoint} has no supporting terrain "
                    + "beneath it inside the level. Dwarves will keep falling.",
                    this);

                continue;
            }

            if (fallDistance < fatalDistance)
            {
                continue;
            }

            Debug.LogWarning(
                $"SpawnPoint at {spawnPoint} has a {fallDistance}-voxel "
                + $"drop. Dwarves die at {fatalDistance} voxels or more. "
                + "Move the SpawnPoint, add terrain beneath it, or adjust "
                + "Fatal Fall Distance on the Dwarf prefab.",
                this);
        }
    }

    private bool ValidateReferences()
    {
        bool valid = true;

        if (pool == null)
        {
            Debug.LogError(
                "DwarfSpawner is missing its DwarfPool reference.",
                this);

            valid = false;
        }

        if (world == null)
        {
            Debug.LogError(
                "DwarfSpawner is missing its VoxelWorld reference.",
                this);

            valid = false;
        }

        return valid;
    }

    private void OnGUI()
    {
        const float uiScale = 2.5f;

        GUI.matrix =
            Matrix4x4.TRS(
                Vector3.zero,
                Quaternion.identity,
                Vector3.one * uiScale);

        const float width = 180f;
        const float height = 40f;
        const float margin = 10f;

        float logicalScreenWidth =
            Screen.width / uiScale;

        float x =
            logicalScreenWidth - width - margin;

        float y = margin;

        if (SimulationState == LevelSimulationState.Preparation)
        {
            if (GUI.Button(
                    new Rect(
                        x,
                        y,
                        width,
                        height),
                    "Start Simulation"))
            {
                StartSimulation();
            }
        }
        else if (SimulationState == LevelSimulationState.Completed)
        {
            DrawResultScreen(
                logicalScreenWidth,
                Screen.height / uiScale);
        }
        else
        {
            float statusWidth =
                Mathf.Min(
                    360f,
                    logicalScreenWidth - margin * 2f);

            float statusX =
                logicalScreenWidth - statusWidth - margin;

            bool targetReached =
                !simulationResolved &&
                IsRescueTargetReached;

            string status =
                simulationResolved
                    ? (Outcome == LevelOutcome.Success
                        ? "LEVEL COMPLETE"
                        : "LEVEL FAILED")
                    : $"Rescued: {rescued}/{GetRequiredRescues()}  "
                      + $"Lost: {died + recalled}  "
                      + $"Active: {rescueGoalProgress.Active}  "
                      + $"Waiting: {rescueGoalProgress.Unspawned}";

            status += $"  Time: {FormatSimulationTime()}";

            if (targetReached)
            {
                status = "✓ TARGET REACHED  " + status;
            }
            else if (!simulationResolved && IsRescueGoalImpossible)
            {
                status += "  (UNSOLVABLE)";
            }

            Color previousColor = GUI.color;

            if (targetReached)
            {
                GUI.color = Color.green;
            }

            GUI.Label(
                new Rect(
                    statusX,
                    y,
                    statusWidth,
                    height),
                status);

            GUI.color = previousColor;

            float nextButtonY = y + height + 4f;

            if (targetReached &&
                !retryConfirmationOpen &&
                !endLevelConfirmationOpen &&
                GUI.Button(
                    new Rect(
                        x,
                        nextButtonY,
                        width,
                        height),
                    "End Level"))
            {
                RequestEndLevel();
            }

            if (targetReached)
            {
                nextButtonY += height + 4f;
            }

            if (SimulationState == LevelSimulationState.Running &&
                !retryConfirmationOpen &&
                !endLevelConfirmationOpen &&
                GUI.Button(
                    new Rect(
                        x,
                        nextButtonY,
                        width,
                        height),
                    "Retry"))
            {
                RequestRetry();
            }

            nextButtonY += height + 4f;

            DrawSimulationSpeedControls(
                x,
                nextButtonY,
                width,
                32f);
        }

        if (retryConfirmationOpen)
        {
            DrawRetryConfirmation(
                logicalScreenWidth,
                Screen.height / uiScale);
        }
        else if (endLevelConfirmationOpen)
        {
            DrawEndLevelConfirmation(
                logicalScreenWidth,
                Screen.height / uiScale);
        }
    }

    private int GetRequiredRescues()
    {
        return Mathf.Clamp(
            requiredRescues,
            1,
            Mathf.Max(1, maxDwarves));
    }

    private void ApplySimulationSpeed()
    {
        Time.timeScale =
            LevelSimulationSpeedUtility.ToTimeScale(
                SimulationSpeed);
    }

    private void DrawSimulationSpeedControls(
        float x,
        float y,
        float width,
        float height)
    {
        const float gap = 4f;
        float buttonWidth = (width - gap * 2f) / 3f;

        DrawSimulationSpeedButton(
            LevelSimulationSpeed.Normal,
            "1×",
            new Rect(x, y, buttonWidth, height));

        DrawSimulationSpeedButton(
            LevelSimulationSpeed.Fast,
            "2×",
            new Rect(
                x + buttonWidth + gap,
                y,
                buttonWidth,
                height));

        DrawSimulationSpeedButton(
            LevelSimulationSpeed.VeryFast,
            "4×",
            new Rect(
                x + (buttonWidth + gap) * 2f,
                y,
                buttonWidth,
                height));
    }

    private void DrawSimulationSpeedButton(
        LevelSimulationSpeed speed,
        string label,
        Rect rect)
    {
        Color previousColor = GUI.color;
        bool previousEnabled = GUI.enabled;

        if (SimulationSpeed == speed)
        {
            GUI.color = Color.green;
        }

        GUI.enabled =
            previousEnabled &&
            SimulationState == LevelSimulationState.Running &&
            !retryConfirmationOpen &&
            !endLevelConfirmationOpen;

        if (GUI.Button(rect, label))
        {
            SetSimulationSpeed(speed);
        }

        GUI.enabled = previousEnabled;
        GUI.color = previousColor;
    }

    private void SetSimulationState(LevelSimulationState state)
    {
        SimulationState = state;

        DwarfJobAssignmentManager assignmentManager =
            FindFirstObjectByType<DwarfJobAssignmentManager>();

        assignmentManager?.SetInteractionEnabled(
            state == LevelSimulationState.Running);

        SimulationStateChanged?.Invoke(state);
    }

    private void DrawRetryConfirmation(
        float logicalScreenWidth,
        float logicalScreenHeight)
    {
        const float width = 320f;
        const float height = 190f;
        const float padding = 14f;

        Rect panel = new(
            (logicalScreenWidth - width) * 0.5f,
            (logicalScreenHeight - height) * 0.5f,
            width,
            height);

        GUI.Box(panel, "Retry Level?");

        GUI.Label(
            new Rect(
                panel.x + padding,
                panel.y + 34f,
                width - padding * 2f,
                76f),
            $"Rescued: {rescued}/{GetRequiredRescues()}\n" +
            $"Active: {pool.ActiveCount}   " +
            $"Lost: {died + recalled}\n" +
            (IsRescueGoalImpossible
                ? "Status: rescue target is no longer reachable.\n"
                : string.Empty) +
            "All progress in this attempt will be reset.");

        float buttonY = panel.yMax - 50f;
        float buttonWidth = (width - padding * 3f) * 0.5f;

        if (GUI.Button(
                new Rect(
                    panel.x + padding,
                    buttonY,
                    buttonWidth,
                    34f),
                "Continue"))
        {
            CancelRetry();
        }

        if (GUI.Button(
                new Rect(
                    panel.x + padding * 2f + buttonWidth,
                    buttonY,
                    buttonWidth,
                    34f),
                "Retry"))
        {
            ConfirmRetry();
        }
    }

    private void RefreshRescueGoalProgress()
    {
        int active = pool != null
            ? pool.ActiveCount
            : 0;

        RescueGoalProgress next = new(
            rescued,
            active,
            Mathf.Max(0, maxDwarves - spawned),
            GetRequiredRescues());

        bool changed =
            next.Rescued != rescueGoalProgress.Rescued ||
            next.Active != rescueGoalProgress.Active ||
            next.Unspawned != rescueGoalProgress.Unspawned ||
            next.Required != rescueGoalProgress.Required;

        rescueGoalProgress = next;

        if (changed)
        {
            RescueGoalProgressChanged?.Invoke(
                rescueGoalProgress);
        }
    }

    private string FormatSimulationTime()
    {
        return FormatSimulationTime(SimulationElapsed);
    }

    private static string FormatSimulationTime(TimeSpan elapsed)
    {
        int totalHours = (int)elapsed.TotalHours;

        return totalHours > 0
            ? $"{totalHours}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
            : $"{elapsed.Minutes}:{elapsed.Seconds:00}";
    }

    private void DrawResultScreen(
        float logicalScreenWidth,
        float logicalScreenHeight)
    {
        if (result == null)
        {
            return;
        }

        const float width = 360f;
        const float height = 300f;
        const float padding = 18f;

        Rect panel = new(
            (logicalScreenWidth - width) * 0.5f,
            (logicalScreenHeight - height) * 0.5f,
            width,
            height);

        string title = result.Outcome == LevelOutcome.Success
            ? "LEVEL COMPLETE"
            : "LEVEL FAILED";

        GUI.Box(panel, title);

        GUI.Label(
            new Rect(
                panel.x + padding,
                panel.y + 38f,
                width - padding * 2f,
                125f),
            $"Rescued: {result.Rescued}/{result.TotalDwarves} " +
            $"({result.RescuePercentage:0.#}%)\n" +
            $"Required: {result.RequiredRescues}\n" +
            $"Lost: {result.Died + result.Recalled}\n" +
            $"Left behind: {result.LeftBehind}\n" +
            $"Simulation time: " +
            $"{FormatSimulationTime(result.SimulationTime)}");

        float buttonY = panel.y + 178f;

        if (!retryConfirmationOpen &&
            GUI.Button(
                new Rect(
                    panel.x + padding,
                    buttonY,
                    width - padding * 2f,
                    38f),
                "Retry Level"))
        {
            RequestRetry();
        }

        bool cityConnected =
            BackToCityRequested != null;

        bool previousEnabled = GUI.enabled;
        GUI.enabled =
            previousEnabled &&
            cityConnected &&
            !retryConfirmationOpen;

        if (GUI.Button(
                new Rect(
                    panel.x + padding,
                    buttonY + 46f,
                    width - padding * 2f,
                    38f),
                "Back to Dwarf City"))
        {
            RequestBackToCity();
        }

        GUI.enabled = previousEnabled;

        if (!cityConnected)
        {
            GUI.Label(
                new Rect(
                    panel.x + padding,
                    panel.yMax - 27f,
                    width - padding * 2f,
                    20f),
                "Dwarf City flow will be connected later.");
        }
    }

    private void DrawEndLevelConfirmation(
        float logicalScreenWidth,
        float logicalScreenHeight)
    {
        const float width = 340f;
        const float height = 190f;
        const float padding = 14f;

        Rect panel = new(
            (logicalScreenWidth - width) * 0.5f,
            (logicalScreenHeight - height) * 0.5f,
            width,
            height);

        GUI.Box(panel, "End Level?");

        GUI.Label(
            new Rect(
                panel.x + padding,
                panel.y + 34f,
                width - padding * 2f,
                84f),
            $"The rescue target has been reached.\n" +
            $"Active dwarves: {rescueGoalProgress.Active}\n" +
            $"Waiting to spawn: {rescueGoalProgress.Unspawned}\n" +
            "Ending now leaves these dwarves behind.");

        float buttonY = panel.yMax - 50f;
        float buttonWidth = (width - padding * 3f) * 0.5f;

        if (GUI.Button(
                new Rect(
                    panel.x + padding,
                    buttonY,
                    buttonWidth,
                    34f),
                "Continue Playing"))
        {
            CancelEndLevel();
        }

        if (GUI.Button(
                new Rect(
                    panel.x + padding * 2f + buttonWidth,
                    buttonY,
                    buttonWidth,
                    34f),
                "End Level"))
        {
            ConfirmEndLevel();
        }
    }

    private void CompleteSimulation(LevelOutcome outcome)
    {
        if (simulationResolved ||
            outcome == LevelOutcome.Undecided)
        {
            return;
        }

        StopAllCoroutines();

        simulationStarted = false;
        simulationResolved = true;
        Outcome = outcome;
        Time.timeScale = 0f;

        result = new LevelAttemptResult(
            outcome,
            maxDwarves,
            GetRequiredRescues(),
            spawned,
            rescued,
            died,
            recalled,
            rescueGoalProgress.Active,
            rescueGoalProgress.Unspawned,
            simulationClock.ElapsedSeconds);

        SetSimulationState(LevelSimulationState.Completed);

    }
}
