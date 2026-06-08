using UnityEngine;

public static class OptionsApplier
{
    // SceneFlow/옵션UI 어디서든 호출해도 안전하게 동작하도록 "Try"로 설계
    public static void TryApplyAll()
    {
        // 대상이 아직 없으면 내부에서 알아서 return 하도록 Apply 함수들이 방어해주는 게 이상적
        GridViewOptions.ApplyToSceneGridRenderer();
        RailBuildOptions.ApplyToSceneRailTool();

        // 앞으로 옵션 늘어나면 여기만 추가
        // CameraOptions.ApplyToSceneCamera();
        // AudioOptions.ApplyToMixer();
        // HudOptions.ApplyToUI();
    }
}