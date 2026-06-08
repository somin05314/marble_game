using UnityEngine;
using UnityEngine.UI;

public class StageButtonStateIcon : MonoBehaviour
{
    [Header("Status Icons")]
    [SerializeField] Image clearedIcon;
    [SerializeField] Image skippedIcon;

    public void SetState(bool isCleared, bool isSkipped)
    {
        if (clearedIcon != null)
            clearedIcon.enabled = isCleared;

        if (skippedIcon != null)
            skippedIcon.enabled = isSkipped;
    }

    public void HideAll()
    {
        if (clearedIcon != null)
            clearedIcon.enabled = false;

        if (skippedIcon != null)
            skippedIcon.enabled = false;
    }
}