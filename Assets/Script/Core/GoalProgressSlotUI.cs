using UnityEngine;
using UnityEngine.UI;

public class GoalProgressSlotUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Image holeImage;
    [SerializeField] Image marbleImage;

    public void SetFilled(bool filled)
    {
        if (marbleImage != null)
            marbleImage.enabled = filled;
    }

    public void SetImmediateEmpty()
    {
        if (marbleImage != null)
            marbleImage.enabled = false;
    }

    public void SetImmediateFilled()
    {
        if (marbleImage != null)
            marbleImage.enabled = true;
    }

    void Reset()
    {
        if (holeImage == null || marbleImage == null)
        {
            var images = GetComponentsInChildren<Image>(true);
            if (images != null && images.Length > 0)
            {
                if (holeImage == null)
                    holeImage = images[0];

                if (images.Length > 1 && marbleImage == null)
                    marbleImage = images[1];
            }
        }
    }
}