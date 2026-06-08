#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class TooltipLocalizationTableImporter
{
    [MenuItem("Tools/Localization/Import Tooltip CSV to SO")]
    public static void ImportCsvToSelectedTable()
    {
        var table = Selection.activeObject as TooltipLocalizationTableSO;
        if (table == null)
        {
            EditorUtility.DisplayDialog(
                "Tooltip CSV Import",
                "먼저 TooltipLocalizationTableSO 에셋을 선택해주세요.",
                "확인");
            return;
        }

        string csvPath = EditorUtility.OpenFilePanel("툴팁 CSV 선택", Application.dataPath, "csv");
        if (string.IsNullOrEmpty(csvPath))
            return;

        try
        {
            string csvText = File.ReadAllText(csvPath);
            List<TooltipLocalizationTableSO.Entry> parsed = ParseCsv(csvText);

            Undo.RecordObject(table, "Import Tooltip CSV");
            table.SetEntries(parsed);
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Tooltip CSV Import",
                $"임포트 완료\n총 {parsed.Count}개 항목",
                "확인");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog(
                "Tooltip CSV Import Error",
                ex.Message,
                "확인");
        }
    }

    static List<TooltipLocalizationTableSO.Entry> ParseCsv(string csvText)
    {
        var rows = ReadCsvRows(csvText);
        if (rows.Count == 0)
            throw new Exception("CSV가 비어 있습니다.");

        var header = rows[0];

        int keyIndex = FindColumn(header, "key");
        int koIndex = FindColumn(header, "ko");
        int enIndex = FindColumn(header, "en");
        int jaIndex = FindColumn(header, "ja");
        int zhCNIndex = FindColumn(header, "zhCN");
        int zhTWIndex = FindColumn(header, "zhTW");
        int ruIndex = FindColumn(header, "ru");
        int deIndex = FindColumn(header, "de");

        if (keyIndex < 0 || koIndex < 0 || enIndex < 0 || jaIndex < 0 || zhCNIndex < 0 ||
    zhTWIndex < 0 || ruIndex < 0 || deIndex < 0)
            throw new Exception("헤더는 반드시 key, ko, en, ja, zhCN, zhTW, ru, de 를 포함해야 합니다.");

        var list = new List<TooltipLocalizationTableSO.Entry>();

        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row == null || row.Count == 0)
                continue;

            string key = GetCell(row, keyIndex);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var entry = new TooltipLocalizationTableSO.Entry
            {
                key = key.Trim(),
                ko = NormalizeCell(GetCell(row, koIndex)),
                en = NormalizeCell(GetCell(row, enIndex)),
                ja = NormalizeCell(GetCell(row, jaIndex)),
                zhCN = NormalizeCell(GetCell(row, zhCNIndex)),
                zhTW = NormalizeCell(GetCell(row, zhTWIndex)),
                ru = NormalizeCell(GetCell(row, ruIndex)),
                de = NormalizeCell(GetCell(row, deIndex)),
            };

            list.Add(entry);
        }

        return list;
    }

    static int FindColumn(List<string> header, string columnName)
    {
        for (int i = 0; i < header.Count; i++)
        {
            if (string.Equals(header[i]?.Trim(), columnName, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    static string GetCell(List<string> row, int index)
    {
        if (index < 0 || index >= row.Count)
            return string.Empty;

        return row[index] ?? string.Empty;
    }

    static string NormalizeCell(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    static List<List<string>> ReadCsvRows(string text)
    {
        var rows = new List<List<string>>();
        var currentRow = new List<string>();
        var currentCell = new System.Text.StringBuilder();

        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        currentCell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    currentCell.Append(c);
                }
            }
            else
            {
                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        break;

                    case ',':
                        currentRow.Add(currentCell.ToString());
                        currentCell.Clear();
                        break;

                    case '\r':
                        break;

                    case '\n':
                        currentRow.Add(currentCell.ToString());
                        currentCell.Clear();
                        rows.Add(currentRow);
                        currentRow = new List<string>();
                        break;

                    default:
                        currentCell.Append(c);
                        break;
                }
            }
        }

        currentRow.Add(currentCell.ToString());

        bool hasAnyValue = false;
        for (int i = 0; i < currentRow.Count; i++)
        {
            if (!string.IsNullOrEmpty(currentRow[i]))
            {
                hasAnyValue = true;
                break;
            }
        }

        if (hasAnyValue || currentRow.Count > 1)
            rows.Add(currentRow);

        return rows;
    }
}
#endif