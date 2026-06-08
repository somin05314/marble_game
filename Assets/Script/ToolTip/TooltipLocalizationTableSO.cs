using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Localization/Tooltip Localization Table")]
public class TooltipLocalizationTableSO : ScriptableObject
{
    public enum Language
    {
        Korean,
        English,
        Japanese,
        ChineseSimplified,
        ChineseTraditional,
        Russian,
        German
    }

    [Serializable]
    public class Entry
    {
        public string key;

        [TextArea] public string ko;
        [TextArea] public string en;
        [TextArea] public string ja;
        [TextArea] public string zhCN;
        [TextArea] public string zhTW;
        [TextArea] public string ru;
        [TextArea] public string de;
    }

    [SerializeField] List<Entry> entries = new List<Entry>();

    Dictionary<string, Entry> _map;

    void OnEnable()
    {
        RebuildMap();
    }

    public void SetEntries(List<Entry> newEntries)
    {
        entries = newEntries ?? new List<Entry>();
        RebuildMap();
    }

    public void RebuildMap()
    {
        _map = new Dictionary<string, Entry>(StringComparer.Ordinal);

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null) continue;
            if (string.IsNullOrWhiteSpace(e.key)) continue;

            string key = e.key.Trim();
            _map[key] = e;
        }
    }

    public bool TryGet(string key, Language lang, out string text)
    {
        text = null;

        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (_map == null)
            RebuildMap();

        key = key.Trim();

        if (!_map.TryGetValue(key, out var e) || e == null)
            return false;

        switch (lang)
        {
            case Language.Korean:
                text = e.ko;
                break;

            case Language.English:
                text = e.en;
                break;

            case Language.Japanese:
                text = e.ja;
                break;

            case Language.ChineseSimplified:
                text = e.zhCN;
                break;

            case Language.ChineseTraditional:
                text = e.zhTW;
                break;

            case Language.Russian:
                text = e.ru;
                break;

            case Language.German:
                text = e.de;
                break;

            default:
                text = e.en;
                break;
        }

        if (string.IsNullOrEmpty(text))
            return false;

        text = text.Replace("\\n", "\n");
        return true;
    }
}