using System.Collections.Generic;
using UnityEngine;

public class DwarfPool : MonoBehaviour
{
    [SerializeField] private DwarfAgent prefab;
    [SerializeField] private int poolSize = 50;

    private Queue<DwarfAgent> pool = new();

    private void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            DwarfAgent dwarf = Instantiate(prefab, transform);
            dwarf.gameObject.SetActive(false);
            pool.Enqueue(dwarf);
        }
    }

    public DwarfAgent Get()
    {
        if (pool.Count == 0)
        {
            Debug.LogWarning("Dwarf pool exhausted, expanding.");
            var extra = Instantiate(prefab, transform);
            extra.gameObject.SetActive(false);
            pool.Enqueue(extra);
        }

        return pool.Dequeue();
    }

    public void Release(DwarfAgent dwarf)
    {
        dwarf.Deactivate();
        pool.Enqueue(dwarf);
    }
}