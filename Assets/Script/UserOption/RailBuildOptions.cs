using System;
using UnityEngine;

public static class RailBuildOptions
{
    const string KEY_CONTINUOUS = "opt_rail_continuous";

    static bool _loaded;
    static bool _continuous;

    public static event Action<bool> OnContinuousChanged;

    public static bool ContinuousPlacement
    {
        get
        {
            EnsureLoaded();
            return _continuous;
        }
        set
        {
            EnsureLoaded();
            if (_continuous == value) return;
            _continuous = value;
            PlayerPrefs.SetInt(KEY_CONTINUOUS, _continuous ? 1 : 0);
            PlayerPrefs.Save();

            // ✅ 씬에 즉시 반영
            ApplyToSceneRailTool();

            OnContinuousChanged?.Invoke(_continuous);
        }
    }

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _continuous = PlayerPrefs.GetInt(KEY_CONTINUOUS, 1) == 1; // 기본 ON
        _loaded = true;
    }

    public static void ApplyToSceneRailTool()
    {
        EnsureLoaded();

        var railTool = UnityEngine.Object.FindFirstObjectByType<RailToolPlacer2D>();
        if (railTool != null)
        {
            // ✅ RailToolPlacer2D에 넣어둔 옵션 적용 API
            railTool.SetContinuousPlacementOption(_continuous, save: false);
        }
    }
}
