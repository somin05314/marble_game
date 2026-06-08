using UnityEngine;
using TMPro;

public class StageSelectEntry : MonoBehaviour
{
    [Header("Visual Objects")]
    [Tooltip("잠김 상태일 때 보여줄 오브젝트")]
    [SerializeField] GameObject lockedVisual;

    [Tooltip("해금 상태일 때 보여줄 오브젝트")]
    [SerializeField] GameObject unlockedVisual;

    [Header("Optional Label")]
    [Tooltip("잠겨 있을 때 숨길 텍스트. 비우면 자식에서 TextMeshProUGUI를 찾음")]
    [SerializeField] TextMeshProUGUI targetLabel;

    public TextMeshProUGUI TargetLabel
    {
        get
        {
            if (targetLabel == null)
                targetLabel = GetComponentInChildren<TextMeshProUGUI>(true);
            return targetLabel;
        }
    }

    public void ApplyVisual(bool visualUnlocked)
    {
        if (lockedVisual != null)
            lockedVisual.SetActive(!visualUnlocked);

        if (unlockedVisual != null)
            unlockedVisual.SetActive(visualUnlocked);

        var label = TargetLabel;
        if (label != null)
            label.gameObject.SetActive(visualUnlocked);
    }
}