using System;
using UnityEngine;
#if STEAMWORKS_NET
using Steamworks;
#endif

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager I { get; private set; }

    const string PREF_LANGUAGE = "LANGUAGE";

    [SerializeField] TooltipLocalizationTableSO table;

    [Header("Runtime")]
    [SerializeField] TooltipLocalizationTableSO.Language language = TooltipLocalizationTableSO.Language.Korean;

    public TooltipLocalizationTableSO.Language CurrentLanguage => language;

    public event Action OnLanguageChanged;

    void Awake()
    {
        if (I != null)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        LoadLanguage();
    }

    public void SetLanguage(TooltipLocalizationTableSO.Language lang)
    {
        if (language == lang)
            return;

        language = lang;

        PlayerPrefs.SetInt(PREF_LANGUAGE, (int)language);
        PlayerPrefs.Save();

        OnLanguageChanged?.Invoke();
    }

    void LoadLanguage()
    {
        // 1. 저장된 언어가 있으면 우선 적용
        if (PlayerPrefs.HasKey(PREF_LANGUAGE))
        {
            language = ClampLanguage(PlayerPrefs.GetInt(PREF_LANGUAGE));
            return;
        }

        // 2. 저장값이 없으면 기본 영어
        language = TooltipLocalizationTableSO.Language.English;

        PlayerPrefs.SetInt(PREF_LANGUAGE, (int)language);
        PlayerPrefs.Save();
    }

    TooltipLocalizationTableSO.Language ClampLanguage(int value)
    {
        int min = 0;
        int max = Enum.GetValues(typeof(TooltipLocalizationTableSO.Language)).Length - 1;
        int clamped = Mathf.Clamp(value, min, max);
        return (TooltipLocalizationTableSO.Language)clamped;
    }

    bool TryGetSteamLanguage(out TooltipLocalizationTableSO.Language lang)
    {
        lang = language;

#if STEAMWORKS_NET
        // Steam이 초기화된 상태에서만 읽기
        if (!SteamManager.Initialized)
            return false;

        string steamLang = SteamApps.GetCurrentGameLanguage();
        if (string.IsNullOrWhiteSpace(steamLang))
            return false;

        steamLang = steamLang.Trim().ToLowerInvariant();

        switch (steamLang)
        {
            case "koreana":
            case "korean":
                lang = TooltipLocalizationTableSO.Language.Korean;
                return true;

            case "english":
                lang = TooltipLocalizationTableSO.Language.English;
                return true;

            case "japanese":
                lang = TooltipLocalizationTableSO.Language.Japanese;
                return true;
        }
#endif

        return false;
    }

    public bool TryGet(string key, out string text)
    {
        text = null;
        if (table == null) return false;

        key = key?.Trim();
        if (string.IsNullOrEmpty(key)) return false;

        return table.TryGet(key, language, out text);
    }

    public string GetText(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        if (table == null)
            return key;

        if (table.TryGet(key, language, out var text))
            return text;

        return key;
    }
}