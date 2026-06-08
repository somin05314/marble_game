using System;
using UnityEngine;

/// <summary>
/// 그리드 켜기/끄기 옵션(PlayerPrefs)을 저장/로드하고,
/// 현재 씬의 GridRenderer에 즉시 반영합니다.
/// </summary>
public static class GridViewOptions
{
    const string KEY_GRID_VISIBLE = "opt_grid_visible";

    static bool _loaded;
    static bool _gridVisible;

    public static event Action<bool> OnGridVisibleChanged;

    public static bool GridVisible
    {
        get
        {
            EnsureLoaded();
            return _gridVisible;
        }
        set
        {
            EnsureLoaded();
            if (_gridVisible == value) return;

            _gridVisible = value;
            PlayerPrefs.SetInt(KEY_GRID_VISIBLE, _gridVisible ? 1 : 0);
            PlayerPrefs.Save();

            ApplyToSceneGridRenderer();
            OnGridVisibleChanged?.Invoke(_gridVisible);
        }
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _gridVisible = PlayerPrefs.GetInt(KEY_GRID_VISIBLE, 1) == 1; // 기본 ON
        _loaded = true;
    }

    /// <summary>
    /// 현재 씬의 GridRenderer에 옵션 값을 적용합니다.
    /// </summary>
    public static void ApplyToSceneGridRenderer()
    {
        EnsureLoaded();

        var gr = UnityEngine.Object.FindFirstObjectByType<GridRenderer>();
        if (gr != null)
            gr.SetUserVisible(_gridVisible);
    }
}
