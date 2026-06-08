using UnityEngine;
using UnityEngine.UI;

public class DisplayOptionPanelUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] Toggle fullscreenToggle;

    [Header("Default Resolution")]
    [SerializeField] int fullscreenWidth = 1920;
    [SerializeField] int fullscreenHeight = 1080;
    [SerializeField] int windowedWidth = 1280;
    [SerializeField] int windowedHeight = 720;

    const string FullscreenKey = "Display_Fullscreen";

    bool _ignoreCallback;

    void Start()
    {
        bool savedFullscreen;

        if (PlayerPrefs.HasKey(FullscreenKey))
            savedFullscreen = PlayerPrefs.GetInt(FullscreenKey) == 1;
        else
            savedFullscreen = true; // 처음 실행 시 기본값: 전체화면

        _ignoreCallback = true;

        if (fullscreenToggle != null)
            fullscreenToggle.isOn = savedFullscreen;

        _ignoreCallback = false;

        ApplyFullscreen(savedFullscreen);

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
    }

    void OnDestroy()
    {
        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
    }

    void OnFullscreenChanged(bool isFullscreen)
    {
        if (_ignoreCallback) return;
        ApplyFullscreen(isFullscreen);
    }

    void ApplyFullscreen(bool isFullscreen)
    {
        if (isFullscreen)
        {
            Screen.SetResolution(fullscreenWidth, fullscreenHeight, true);
        }
        else
        {
            Screen.SetResolution(windowedWidth, windowedHeight, false);
        }

        PlayerPrefs.SetInt(FullscreenKey, isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }
}