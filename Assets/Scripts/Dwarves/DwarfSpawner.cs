using System.Collections;
using UnityEngine;

public class DwarfSpawner : MonoBehaviour
{
    [SerializeField] private DwarfPool pool;
    [SerializeField] private VoxelWorld world;

    [SerializeField] private Vector3Int spawnPosition;
    [SerializeField] private float spawnInterval = 1.0f;
    [SerializeField] private int maxDwarves = 20;

    private bool simulationStarted;

    private int spawned;

    public void StartSimulation()
    {
        world.ScanSpawnPoints();

        if (simulationStarted)
            return;

        simulationStarted = true;

        if (world.GetSpawnPoints().Count == 0)
        {
            Debug.LogError("No SpawnPoint found in level.");
            return;
        }

        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while (spawned < maxDwarves)
        {
            SpawnDwarf();
            spawned++;

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnDwarf()
    {
        DwarfAgent dwarf = pool.Get();

        dwarf.gameObject.SetActive(true);

        Vector3Int spawnVoxel = world.GetSpawnPoint();

        dwarf.Activate(spawnVoxel);

        // optional: align to terrain later
    }

    private void OnGUI()
    {
        GUI.matrix = Matrix4x4.TRS(
            Vector3.zero,
            Quaternion.identity,
            Vector3.one * 2.5f
        );

        const float width = 180f;
        const float height = 40f;
        const float margin = 10f;

        float x = (Screen.width / 2.5f) - width - margin;
        float y = margin;

        if (!simulationStarted)
        {
            if (GUI.Button(new Rect(x, y, width, height), "Start Simulation"))
            {
                StartSimulation();
            }
        }
    }
}