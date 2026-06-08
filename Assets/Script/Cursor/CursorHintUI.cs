using UnityEngine;
using UnityEngine.EventSystems;

public class CursorHintUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public CursorManager.CursorState enterState = CursorManager.CursorState.Hand;

    public void OnPointerEnter(PointerEventData eventData)
    {
        CursorRequestStack.Push(this, enterState);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        CursorRequestStack.Pop(this);
    }
}