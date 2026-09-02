using System.Collections;
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
    private int nextSpawnPointIndex;
    private bool spawnFinished;
    private bool simulationResolved;

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
        if (simulationStarted)
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

        spawned = 0;
        rescued = 0;
        died = 0;
        nextSpawnPointIndex = 0;
        spawnFinished = false;
        simulationResolved = false;
        simulationStarted = true;

        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while (spawned < maxDwarves)
        {
            if (TrySpawnDwarf())
            {
                spawned++;

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
        else
        {
            died++;
        }

        TryResolveSimulation();
    }

    private void TryResolveSimulation()
    {
        if (!spawnFinished ||
            rescued + died < spawned)
        {
            return;
        }

        simulationResolved = true;

        bool victory =
            rescued >= GetRequiredRescues();

        Debug.Log(
            victory
                ? $"Level complete! Rescued {rescued}/{spawned} dwarves."
                : $"Level failed. Rescued {rescued}/{spawned} dwarves; "
                  + $"required {GetRequiredRescues()}.");
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

            dwarf.Activate(
                spawnVoxel,
                initialFacing);

            nextSpawnPointIndex =
                (index + 1)
                % spawnPoints.Count;

            Debug.Log(
                $"Spawned {dwarf.name} at anchor "
                + $"{spawnVoxel}, facing {initialFacing}.");

            return true;
        }

        return false;
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
        GUI.matrix =
            Matrix4x4.TRS(
                Vector3.zero,
                Quaternion.identity,
                Vector3.one * 2.5f);

        const float width = 180f;
        const float height = 40f;
        const float margin = 10f;

        float x =
            (Screen.width / 2.5f)
            - width
            - margin;

        float y = margin;

        if (!simulationStarted)
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
            string status =
                simulationResolved
                    ? (rescued >= GetRequiredRescues()
                        ? "LEVEL COMPLETE"
                        : "LEVEL FAILED")
                    : $"Rescued: {rescued}/{GetRequiredRescues()}  "
                      + $"Lost: {died}  Active: {pool.ActiveCount}";

            GUI.Label(
                new Rect(
                    x,
                    y,
                    width * 1.8f,
                    height),
                status);
        }
    }

    private int GetRequiredRescues()
    {
        return Mathf.Clamp(
            requiredRescues,
            1,
            Mathf.Max(1, maxDwarves));
    }
}
