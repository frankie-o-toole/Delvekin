using System.Collections;
using System.Collections.Generic;
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
    private float timeScaleBeforePause = 1f;
    private bool retryConfirmationOpen;
    private LevelSimulationState stateBeforeRetryConfirmation;
    private RescueGoalProgress rescueGoalProgress;

    public LevelSimulationState SimulationState { get; private set; } =
        LevelSimulationState.Preparation;

    public LevelOutcome Outcome { get; private set; } =
        LevelOutcome.Undecided;

    public event System.Action<LevelSimulationState>
        SimulationStateChanged;

    public event System.Action<RescueGoalProgress>
        RescueGoalProgressChanged;

    public int TotalDwarves => maxDwarves;
    public int RequiredRescues => GetRequiredRescues();
    public RescueGoalProgress RescueGoalProgress =>
        rescueGoalProgress;
    public bool IsRescueGoalImpossible =>
        rescueGoalProgress.IsImpossible;
    public bool IsRescueTargetReached =>
        rescueGoalProgress.TargetReached;

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

        RefreshRescueGoalProgress();

        SetSimulationState(LevelSimulationState.Running);

        world.StartFluidSimulation();

        StartCoroutine(SpawnLoop());
    }

    public void ResetSimulation()
    {
        StopAllCoroutines();

        Time.timeScale = 1f;
        timeScaleBeforePause = 1f;
        retryConfirmationOpen = false;

        // Disable result accounting before recalling active dwarves.
        simulationStarted = false;
        simulationResolved = false;
        spawnFinished = false;
        Outcome = LevelOutcome.Undecided;

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

        timeScaleBeforePause = Mathf.Max(0.01f, Time.timeScale);
        Time.timeScale = 0f;
        SetSimulationState(LevelSimulationState.Paused);
    }

    public void ResumeSimulation()
    {
        if (SimulationState != LevelSimulationState.Paused)
        {
            return;
        }

        Time.timeScale = timeScaleBeforePause;
        SetSimulationState(LevelSimulationState.Running);
    }

    public void RequestRetry()
    {
        if (retryConfirmationOpen ||
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

    public void EndLevel()
    {
        if (SimulationState != LevelSimulationState.Running ||
            !IsRescueTargetReached)
        {
            return;
        }

        CompleteSimulation(LevelOutcome.Success);
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

        if (reason == DwarfReleaseReason.Rescued)
        {
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

            Debug.Log(
                $"Spawned {dwarf.name} at anchor "
                + $"{spawnVoxel}, facing {spawnFacing}.");

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
                GUI.Button(
                    new Rect(
                        x,
                        nextButtonY,
                        width,
                        height),
                    "End Level"))
            {
                EndLevel();
            }

            if (targetReached)
            {
                nextButtonY += height + 4f;
            }

            if ((SimulationState == LevelSimulationState.Running ||
                 SimulationState == LevelSimulationState.Completed) &&
                !retryConfirmationOpen &&
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
        }

        if (retryConfirmationOpen)
        {
            DrawRetryConfirmation(
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

        SetSimulationState(LevelSimulationState.Completed);

        Debug.Log(
            outcome == LevelOutcome.Success
                ? $"Level complete! Rescued {rescued}/{spawned} dwarves."
                : $"Level failed. Rescued {rescued}/{spawned} dwarves; "
                  + $"required {GetRequiredRescues()}.");
    }
}
