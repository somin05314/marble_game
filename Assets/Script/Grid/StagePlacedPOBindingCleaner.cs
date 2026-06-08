using UnityEngine;

public class StagePlacedPOBindingCleaner : MonoBehaviour
{
    [Header("Target Root (Optional)")]
    [Tooltip("비워두면 씬 전체의 PlacementObject를 검사합니다.")]
    [SerializeField] Transform targetRoot;

    [Header("GridPlacer")]
    [Tooltip("미리 배치된 PO의 Rail binding을 초기 1회 확정할 GridPlacer")]
    [SerializeField] GridPlacer gridPlacer;

    [Header("Run Timing")]
    [Tooltip("Start에서 1프레임 기다린 뒤 실행할지 여부")]
    [SerializeField] bool runAfterOneFrame = true;

    [Header("Init Options")]
    [Tooltip("실제 연결 레일이 없는 stale binding은 먼저 제거")]
    [SerializeField] bool clearStaleBindingFirst = true;

    [Tooltip("미리 배치된 PO를 시작 시 1회 초기화")]
    [SerializeField] bool initializeStagePlacedPOs = true;

    [Header("Debug")]
    [SerializeField] bool debugLog = true;

    void Start()
    {
        if (runAfterOneFrame)
            StartCoroutine(CoCleanupNextFrame());
        else
            CleanupNow();
    }

    System.Collections.IEnumerator CoCleanupNextFrame()
    {
        yield return null;
        CleanupNow();
    }

    [ContextMenu("Cleanup Now")]
    public void CleanupNow()
    {
        if (gridPlacer == null)
            gridPlacer = FindFirstObjectByType<GridPlacer>();

        PlacementObject[] allPOs;

        if (targetRoot != null)
            allPOs = targetRoot.GetComponentsInChildren<PlacementObject>(true);
        else
            allPOs = FindObjectsByType<PlacementObject>(FindObjectsSortMode.None);

        int cleanedCount = 0;
        int initializedCount = 0;

        for (int i = 0; i < allPOs.Length; i++)
        {
            var po = allPOs[i];
            if (po == null) continue;

            bool cleaned = false;

            if (clearStaleBindingFirst)
                cleaned = TryClearStaleBinding(po);

            if (cleaned)
                cleanedCount++;

            if (initializeStagePlacedPOs)
            {
                if (TryInitializeStagePlacedPO(po))
                    initializedCount++;
            }
        }

        if (debugLog)
        {
            Debug.Log(
                $"[StagePlacedPOBindingCleaner] cleaned stale bindings: {cleanedCount}, " +
                $"initialized stage-placed POs: {initializedCount}"
            );
        }
    }

    bool TryInitializeStagePlacedPO(PlacementObject po)
    {
        if (po == null) return false;
        if (gridPlacer == null) return false;

        // 자동 바인딩 안 쓰는 PO는 건너뜀
        if (!po.AutoRailAttach)
            return false;

        var bind = po.GetComponent<RailNodeFollowBinding2D>();

        // 이미 유효한 바인딩이 있으면 다시 만들지 않음
        if (bind != null && HasAnyConnectedRail(bind))
        {
            if (debugLog)
                Debug.Log($"[StagePlacedPOBindingCleaner] skip init (already connected) -> {po.name}", po);
            return false;
        }

        if (debugLog)
            Debug.Log($"[StagePlacedPOBindingCleaner] initialize stage-placed PO -> {po.name}", po);

        // 시작 시점 1회만 초기화
        return true;
    }

    bool TryClearStaleBinding(PlacementObject po)
    {
        if (po == null) return false;

        var bind = po.GetComponent<RailNodeFollowBinding2D>();
        if (bind == null) return false;

        bool hasAnyBindingInfo = HasAnyBindingInfo(bind);
        if (!hasAnyBindingInfo)
            return false;

        bool hasConnectedRail = HasAnyConnectedRail(bind);
        if (hasConnectedRail)
            return false;

        if (debugLog)
            Debug.Log($"[StagePlacedPOBindingCleaner] clear stale binding -> {po.name}", po);

        RailNodeSnapBinder.Detach(po);

        bind.node = null;
        bind.anchorPoint = null;
        bind.builtRevision = 0;
        bind.builtRadius = 0f;
        bind.builtMaskValue = 0;

        return true;
    }

    bool HasAnyBindingInfo(RailNodeFollowBinding2D bind)
    {
        if (bind == null) return false;

        var entries = bind.Entries;
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.node != null) return true;
                if (e.anchorPoint != null) return true;
            }
        }

        if (bind.node != null) return true;
        if (bind.anchorPoint != null) return true;

        return false;
    }

    bool HasAnyConnectedRail(RailNodeFollowBinding2D bind)
    {
        if (bind == null) return false;

        var entries = bind.Entries;
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var node = entries[i].node;
                if (node == null) continue;

                if (node.GetConnectedRailCount() > 0)
                    return true;
            }
        }

        if (bind.node != null && bind.node.GetConnectedRailCount() > 0)
            return true;

        return false;
    }
}