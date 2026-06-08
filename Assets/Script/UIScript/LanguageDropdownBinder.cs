using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LanguageDropdownBinder : MonoBehaviour
{
    [SerializeField] TMP_Dropdown dropdown;

    bool _ignoreCallback;

    void Awake()
    {
        if (dropdown == null)
            dropdown = GetComponent<TMP_Dropdown>();

        if (dropdown == null)
            return;

        SetupOptions();
        SyncFromManager();

        dropdown.onValueChanged.AddListener(OnDropdownChanged);
    }

    void OnEnable()
    {
        SyncFromManager();

        if (LocalizationManager.I != null)
            LocalizationManager.I.OnLanguageChanged += SyncFromManager;
    }

    void OnDisable()
    {
        if (LocalizationManager.I != null)
            LocalizationManager.I.OnLanguageChanged -= SyncFromManager;
    }

    void SetupOptions()
    {
        dropdown.ClearOptions();

        dropdown.AddOptions(new List<string>
    {
        "한국어",
        "English",
        "日本語",
        "简体中文",
        "繁體中文",
        "Русский",
        "Deutsch"
    });
    }

    void SyncFromManager()
    {
        if (dropdown == null || LocalizationManager.I == null)
            return;

        _ignoreCallback = true;
        dropdown.value = (int)LocalizationManager.I.CurrentLanguage;
        dropdown.RefreshShownValue();
        _ignoreCallback = false;
    }

    void OnDropdownChanged(int index)
    {
        if (_ignoreCallback)
            return;

        if (LocalizationManager.I == null)
            return;

        LocalizationManager.I.SetLanguage((TooltipLocalizationTableSO.Language)index);
    }
}