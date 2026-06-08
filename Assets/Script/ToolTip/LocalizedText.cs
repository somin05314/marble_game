using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] string localizationKey;

    [Header("Target")]
    [SerializeField] TMP_Text tmpText;
    [SerializeField] Text legacyText;

    [Header("Fallback")]
    [SerializeField] string fallbackText;

    void Reset()
    {
        tmpText = GetComponent<TMP_Text>();
        legacyText = GetComponent<Text>();
    }

    void OnEnable()
    {
        if (LocalizationManager.I != null)
            LocalizationManager.I.OnLanguageChanged += Refresh;

        Refresh();
    }

    void Start()
    {
        Refresh();
    }

    void OnDisable()
    {
        if (LocalizationManager.I != null)
            LocalizationManager.I.OnLanguageChanged -= Refresh;
    }

    public void Refresh()
    {
        string result = fallbackText;

        if (LocalizationManager.I != null &&
            !string.IsNullOrWhiteSpace(localizationKey) &&
            LocalizationManager.I.TryGet(localizationKey, out var localized))
        {
            result = localized;
        }

        if (tmpText != null)
            tmpText.text = result;

        if (legacyText != null)
            legacyText.text = result;
    }

    public void SetKey(string newKey)
    {
        localizationKey = newKey;
        Refresh();
    }
}