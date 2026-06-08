using UnityEngine;
using UnityEngine.UI;

public class ClearPlacedButtonBridge : MonoBehaviour
{
    [SerializeField] Button button; // 인스펙터에 연결

    void Awake()
    {
        if (button == null) button = GetComponent<Button>();
    }

    void OnEnable()
    {
        GameModeManager.OnModeChanged += HandleModeChanged;

        // 처음 상태 반영
        var gmm = GameModeManager.Instance;
        HandleModeChanged(gmm != null ? gmm.currentMode : GameMode.Build);
    }

    void OnDisable()
    {
        GameModeManager.OnModeChanged -= HandleModeChanged;
    }

    void HandleModeChanged(GameMode mode)
    {
        if (button == null) return;

        bool enable = (mode == GameMode.Build);
        button.interactable = enable;

        // 원하면 플레이 중 숨기기까지:
        // button.gameObject.SetActive(enable);
    }

    public void OnClickClearPlaced()
    {
        // ✅ Play 중 눌림 방지(보험)
        var gmm = GameModeManager.Instance;
        if (gmm != null && gmm.currentMode != GameMode.Build)
            return;

        var psm = Object.FindFirstObjectByType<PuzzleSnapshotManager>();
        if (psm == null)
        {
            Debug.LogWarning("[UI] PuzzleSnapshotManager not found (Core not loaded?)");
            return;
        }

        psm.ClearPlacedNow();
    }
}