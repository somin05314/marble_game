using UnityEngine;
using UnityEngine.UI;

public class OptionsPanelController : MonoBehaviour
{
    public static OptionsPanelController I { get; private set; }

    [SerializeField] GameObject panel; // OptionsPanel

    [Header("Menu Buttons")]
    [SerializeField] Button returnToTitleButton;

    public bool IsOpen => panel != null && panel.activeSelf;

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;

        if (panel != null) panel.SetActive(false);
    }

    public void Open()
    {
        if (panel == null) return;

        panel.SetActive(true);
        RefreshReturnToTitleButton();
    }

    public void Close()
    {
        if (panel == null) return;
        panel.SetActive(false);
    }

    public void Toggle()
    {
        if (panel == null) return;

        bool nextOpen = !panel.activeSelf;
        panel.SetActive(nextOpen);

        if (nextOpen)
            RefreshReturnToTitleButton();
    }

    void RefreshReturnToTitleButton()
    {
        if (returnToTitleButton == null) return;

        bool show =
            SceneFlow.I != null &&
            !SceneFlow.I.IsCurrentStartScene();

        returnToTitleButton.gameObject.SetActive(show);
    }

    public void OnClickReturnToTitle()
    {
        SceneFlow.I?.GoStart();
    }
}