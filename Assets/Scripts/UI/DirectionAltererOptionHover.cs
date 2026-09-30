using UnityEngine;
using UnityEngine.EventSystems;

public sealed class DirectionAltererOptionHover :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private DwarfJobAssignmentManager assignmentManager;
    private DirectionAltererTurn turn;

    public void Initialize(
        DwarfJobAssignmentManager manager,
        DirectionAltererTurn previewTurn)
    {
        assignmentManager = manager;
        turn = previewTurn;
    }

    public void Clear()
    {
        assignmentManager
            ?.PreviewDirectionAltererTurn(null);

        assignmentManager = null;
    }

    public void OnPointerEnter(
        PointerEventData eventData)
    {
        assignmentManager
            ?.PreviewDirectionAltererTurn(turn);
    }

    public void OnPointerExit(
        PointerEventData eventData)
    {
        assignmentManager
            ?.PreviewDirectionAltererTurn(null);
    }

    private void OnDisable()
    {
        assignmentManager
            ?.PreviewDirectionAltererTurn(null);
    }
}
