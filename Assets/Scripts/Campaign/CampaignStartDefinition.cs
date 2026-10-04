using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CampaignStartDefinition",
    menuName = "Delvekin/Campaign Start Definition")]
public sealed class CampaignStartDefinition : ScriptableObject
{
    [Min(0)]
    [SerializeField]
    private int startingOre;

    [SerializeField]
    private List<DwarfJobType> startingUnlockedJobs = new()
    {
        DwarfJobType.DirectionAlter
    };

    public int StartingOre => Mathf.Max(0, startingOre);

    public IReadOnlyList<DwarfJobType> StartingUnlockedJobs =>
        startingUnlockedJobs;

    public CampaignSaveData CreateNewCampaign()
    {
        return CampaignSaveData.CreateNew(
            startingUnlockedJobs,
            StartingOre);
    }

    private void OnValidate()
    {
        startingOre = Mathf.Max(0, startingOre);
        startingUnlockedJobs ??= new List<DwarfJobType>();
    }
}
