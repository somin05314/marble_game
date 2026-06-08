using System;
using UnityEngine;

public enum SnapPointRole
{
    AnchorRoot,     // ✅ 설치 가능 점(맵에 미리 배치된 점)
    Connector       // ✅ 블록에 달린 연결 편의 점
}

public class SnapPoint : MonoBehaviour
{
    public SnapPointRole role = SnapPointRole.Connector;
    public bool IsAnchorRoot => role == SnapPointRole.AnchorRoot;

    [Header("Visibility")]
    [Tooltip("Play 모드에서는 SnapPoint의 시각 표시(스프라이트/렌더러)를 숨깁니다.")]
    public bool hideVisualInPlayMode = true;

    [Tooltip("Build 모드에서도 평소에는 숨기고, 레일 설치/드래그 중일 때만 표시합니다.")]
    public bool showOnlyWhileRailEditing = true;

    [Tooltip("추가로 PO 드래그 중에도 SnapPoint를 표시합니다.")]
    public bool showWhilePODragging = true;

    [Tooltip("비워두면 GameModeManager.Instance 또는 씬에서 자동 탐색")]
    public GameModeManager gameMode;

    [Tooltip("비워두면 씬에서 자동 탐색")]
    public GridPlacer gridPlacer;

    [Header("Snap ID (Legacy)")]
    public int snapId;          // 기존 방식(폴백용)

    [Header("Stable ID (Recommended)")]
    [SerializeField] string stableGuid;   // ✅ 저장/복원은 이걸로
    public string StableGuid => stableGuid;

    public float snapRadius = 1f;
    public float allowedPenetration = 1f;

    [HideInInspector] public SnapRoot root;

    Renderer[] _renderers;

    bool _lastVisible;
    bool _initializedVisibility;

    void Awake()
    {
        if (gameMode == null)
            gameMode = GameModeManager.Instance ?? FindFirstObjectByType<GameModeManager>();

        if (gridPlacer == null)
            gridPlacer = FindFirstObjectByType<GridPlacer>();

        _renderers = GetComponentsInChildren<Renderer>(true);
        RefreshVisibility(force: true);
    }

    void LateUpdate()
    {
        RefreshVisibility(force: false);
    }

    bool IsPlayMode()
    {
        if (!hideVisualInPlayMode) return false;

        if (gameMode == null)
            gameMode = GameModeManager.Instance ?? FindFirstObjectByType<GameModeManager>();

        if (gameMode == null) return false;
        return gameMode.currentMode == GameMode.Play;
    }

    bool IsRailEditingNow()
    {
        bool railEditing = !showOnlyWhileRailEditing || RailToolPlacer2D.IsShowingSnapPointGuides;
        if (railEditing) return true;

        if (!showWhilePODragging) return false;

        if (gridPlacer == null)
            gridPlacer = FindFirstObjectByType<GridPlacer>();

        if (gridPlacer == null) return false;

        return gridPlacer.IsDraggingSelectedPO || gridPlacer.IsDragCandidateSelectedPO;
    }

    bool ComputeShouldShow()
    {
        // 1. Play 모드에서는 숨김
        if (hideVisualInPlayMode && IsPlayMode())
            return false;

        // 2. Build 모드에서도 레일 편집 중 / PO 드래그 중일 때만 보이기
        if (showOnlyWhileRailEditing && !IsRailEditingNow())
            return false;

        return true;
    }

    void RefreshVisibility(bool force)
    {
        if (_renderers == null || _renderers.Length == 0) return;

        bool show = ComputeShouldShow();

        if (!force && _initializedVisibility && _lastVisible == show)
            return;

        _initializedVisibility = true;
        _lastVisible = show;
        ApplyVisibility(show);
    }

    void ApplyVisibility(bool show)
    {
        if (_renderers == null || _renderers.Length == 0) return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;
            if (r.enabled != show)
                r.enabled = show;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(stableGuid))
        {
            stableGuid = Guid.NewGuid().ToString("N");
            return;
        }

        if (HasDuplicateGuidInSamePlacementObject(stableGuid))
        {
            stableGuid = Guid.NewGuid().ToString("N");
        }
    }

    bool HasDuplicateGuidInSamePlacementObject(string guid)
    {
        var po = GetComponentInParent<PlacementObject>();
        if (po == null) return false;

        var points = po.GetComponentsInChildren<SnapPoint>(true);

        int count = 0;
        foreach (var p in points)
        {
            if (p != null && p.stableGuid == guid)
                count++;
        }
        return count > 1;
    }
#endif
}