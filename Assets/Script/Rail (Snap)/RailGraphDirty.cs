using UnityEngine;

public class RailGraphDirty : MonoBehaviour
{
    public static bool dirty;

    public static void MarkDirty()
    {
        dirty = true;
    }

    void LateUpdate()
    {
        if (!dirty) return;
        dirty = false;

        // 너희 프로젝트에 이미 있는 전체 정리/리빌드
        RailGraphCleanup2D.Cleanup();

        // (있다면) 스냅 후보 캐시/MoveHint도 같이 리빌드
        // SnapManager.RebuildAll();
        // POMoveRailHint2D.RebuildAll();
    }
}
