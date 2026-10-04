using UnityEngine;
using UnityEngine.EventSystems;

public static class PointerUiUtility
{
    public static bool IsPointerOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return true;
        }

        return DwarfSpawner.IsPointerOverRuntimeUI(screenPosition);
    }
}
