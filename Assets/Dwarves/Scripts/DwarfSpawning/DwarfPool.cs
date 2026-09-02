using System.Collections.Generic;
using System;
using UnityEngine;

public enum DwarfReleaseReason
{
    Died,
    Rescued
}

public class DwarfPool : MonoBehaviour
{
    [SerializeField]
    private DwarfAgent prefab;

    [SerializeField]
    private int poolSize = 50;

    private readonly Queue<DwarfAgent> availableDwarves =
        new();

    private readonly HashSet<DwarfAgent> activeDwarves =
        new();

    public IReadOnlyCollection<DwarfAgent> ActiveDwarves =>
        activeDwarves;

    public int AvailableCount =>
        availableDwarves.Count;

    public int ActiveCount =>
        activeDwarves.Count;

    public event Action<DwarfAgent, DwarfReleaseReason>
        DwarfReleased;

    private void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            DwarfAgent dwarf =
                CreateDwarf();

            availableDwarves.Enqueue(dwarf);
        }
    }

    public DwarfAgent Get()
    {
        if (availableDwarves.Count == 0)
        {
            Debug.LogWarning(
                "Dwarf pool exhausted. Expanding pool.");

            DwarfAgent extra =
                CreateDwarf();

            availableDwarves.Enqueue(extra);
        }

        DwarfAgent dwarf =
            availableDwarves.Dequeue();

        activeDwarves.Add(dwarf);

        return dwarf;
    }

    public void Release(
        DwarfAgent dwarf,
        DwarfReleaseReason reason)
    {
        if (dwarf == null)
        {
            return;
        }

        if (!activeDwarves.Remove(dwarf))
        {
            Debug.LogWarning(
                $"Attempted to release {dwarf.name}, "
                + "but it was not registered as active.");

            return;
        }

        DwarfReleased?.Invoke(
            dwarf,
            reason);

        dwarf.Deactivate();
        availableDwarves.Enqueue(dwarf);
    }

    private DwarfAgent CreateDwarf()
    {
        DwarfAgent dwarf =
            Instantiate(
                prefab,
                transform);

        dwarf.gameObject.SetActive(false);

        return dwarf;
    }
}
