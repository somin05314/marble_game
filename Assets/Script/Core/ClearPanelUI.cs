using UnityEngine;

public class ClearPanelUI : MonoBehaviour
{
    [SerializeField] GameObject clearPanel; // "StageSelect로" 버튼 들어있는 패널

    void OnEnable()
    {
        GameModeManager.OnStageCleared += Show;
    }

    void OnDisable()
    {
        GameModeManager.OnStageCleared -= Show;
    }

    void Start()
    {
        if (clearPanel != null) clearPanel.SetActive(false);
    }

    void Show()
    {
        if (clearPanel != null) clearPanel.SetActive(true);
    }

    public void Hide()
    {
        if (clearPanel != null) clearPanel.SetActive(false);
    }

    // ✅ 버튼: "계속 빌드" (패널만 끄기)
    public void OnClickClose()
    {
        Hide();
    }
}
