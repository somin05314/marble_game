using TMPro;
using UnityEngine;

public class InteractionHintUI : MonoBehaviour
{
    public static InteractionHintUI I { get; private set; }

    const string HintEnabledKey = "UI_InteractionHint";

    [Header("Refs")]
    [SerializeField] GameObject root;
    [SerializeField] TMP_Text label;
    [SerializeField] RectTransform backgroundPanel;

    [Header("Background Padding")]
    [SerializeField] Vector2 padding = new Vector2(24f, 14f);

    [Header("Default Option")]
    [SerializeField] bool defaultEnabled = true;

    bool _isPlacementMode = true;
    bool _isEnabled = true;
    string _current;

    public bool IsEnabled => _isEnabled;

    void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;

        LoadOption();
        SetHint(null);
        RefreshVisible();
    }

    public void SetPlacementMode(bool isPlacementMode)
    {
        _isPlacementMode = isPlacementMode;
        RefreshVisible();
    }

    public void SetHint(string text)
    {
        _current = text;

        if (label != null)
            label.text = text ?? "";

        RefreshVisible();
    }

    public void SetEnabledOption(bool enabled)
    {
        if (_isEnabled == enabled)
            return;

        _isEnabled = enabled;
        SaveOption();
        RefreshVisible();
    }

    public void ToggleEnabledOption()
    {
        SetEnabledOption(!_isEnabled);
    }

    void RefreshVisible()
    {
        bool show = _isEnabled && _isPlacementMode && !string.IsNullOrEmpty(_current);

        if (root != null)
            root.SetActive(show);

        if (show)
            RefreshBackgroundSize();
    }

    void RefreshBackgroundSize()
    {
        if (label == null || backgroundPanel == null)
            return;

        label.ForceMeshUpdate();

        Vector2 textSize = label.GetRenderedValues(false);

        backgroundPanel.sizeDelta = new Vector2(
            textSize.x + padding.x,
            textSize.y + padding.y
        );
    }

    void SaveOption()
    {
        PlayerPrefs.SetInt(HintEnabledKey, _isEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    void LoadOption()
    {
        _isEnabled = PlayerPrefs.GetInt(HintEnabledKey, defaultEnabled ? 1 : 0) == 1;
    }
}