using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RailSnapNode2D : MonoBehaviour
{
    [Header("Capacity")]
    [SerializeField, Min(0)]
    int capacityOverride = 0; // 0이면 "기본값 사용"

    public int CapacityOverride => capacityOverride;

    [SerializeField] bool isAnchor = true;
    public bool IsAnchor => isAnchor;

    public void SetAnchor(bool v)
    {
        isAnchor = v;
        RefreshAnchorArrow();
    }

    public void SetCapacityOverride(int cap)
    {
        capacityOverride = Mathf.Max(0, cap);
        RefreshAnchorArrow();
    }

    [Header("Anchor Arrow Visual")]
    [Tooltip("앵커가 비어 있을 때 표시할 위쪽 화살표 루트")]
    [SerializeField] GameObject anchorArrowVisual;

    [Tooltip("실행 모드(시뮬레이션)일 때 화살표를 숨김")]
    [SerializeField] bool hideArrowInRunMode = true;

    [Header("Arrow Pulse")]
    [Tooltip("화살표가 커지는 최대 배율")]
    [SerializeField] float arrowPulseMaxScale = 1.2f;

    [Tooltip("펄스 1회 왕복 시간(초)")]
    [SerializeField] float arrowPulseDuration = 0.8f;

    bool _isRunMode = false;

    [Header("PO Block")]
    [SerializeField] bool blockPOPlacement = false;
    public bool BlockPOPlacement => blockPOPlacement;

    [SerializeField, Min(0)] int poBlockRadiusCells = 1; // 1이면 3x3
    public int POBlockRadiusCells => poBlockRadiusCells;



    Vector3 _arrowBaseScale = Vector3.one;
    Coroutine _arrowPulseRoutine;

    // =============================
    // Connected Rails (runtime)
    // =============================
    readonly HashSet<RailSpan2D> _connectedRails = new HashSet<RailSpan2D>();

    public void RegisterRail(RailSpan2D rail)
    {
        if (rail == null) return;
        _connectedRails.Add(rail);
        RefreshAnchorArrow();
    }

    public void UnregisterRail(RailSpan2D rail)
    {
        if (rail == null) return;
        _connectedRails.Remove(rail);
        RefreshAnchorArrow();
    }

    /// <summary>
    /// 현재 노드에 연결된 레일 개수. (exclude가 있으면 그 레일은 제외)
    /// </summary>
    public int GetConnectedRailCount(RailSpan2D exclude = null, bool cleanupNulls = true)
    {
        if (cleanupNulls)
            _connectedRails.RemoveWhere(r => r == null);

        if (exclude == null) return _connectedRails.Count;

        int c = 0;
        foreach (var r in _connectedRails)
        {
            if (r == null) continue;
            if (r == exclude) continue;
            c++;
        }
        return c;
    }

    /// <summary>
    /// 외부(GameModeManager 등)에서 실행 모드 여부를 넣어줌
    /// true = 실행 모드, false = 배치 모드
    /// </summary>
    public void SetRunMode(bool isRunMode)
    {
        _isRunMode = isRunMode;
        RefreshAnchorArrow();
    }

    [Header("Default Capacity Fallback")]
    [SerializeField, Min(1)] int defaultCapacity = 1;

    void RefreshAnchorArrow()
    {
        if (anchorArrowVisual == null) return;

        int connected = GetConnectedRailCount();
        int capacity = GetCapacity(defaultCapacity);

        bool hasRoom = connected < capacity;
        bool shouldShow = isAnchor && hasRoom;

        if (hideArrowInRunMode && _isRunMode)
            shouldShow = false;

        if (anchorArrowVisual.activeSelf != shouldShow)
            anchorArrowVisual.SetActive(shouldShow);

        // ✅ 플레이 중에만 펄스 처리
        if (Application.isPlaying)
            UpdateArrowPulse(shouldShow);
        else if (anchorArrowVisual != null)
            anchorArrowVisual.transform.localScale = _arrowBaseScale;
    }

    void UpdateArrowPulse(bool shouldShow)
    {
        if (anchorArrowVisual == null)
            return;

        // ✅ 에디터/비활성 상태에서는 코루틴 돌리지 않음
        if (!Application.isPlaying || !isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            if (_arrowPulseRoutine != null)
            {
                StopCoroutine(_arrowPulseRoutine);
                _arrowPulseRoutine = null;
            }

            anchorArrowVisual.transform.localScale = _arrowBaseScale;
            return;
        }

        if (shouldShow)
        {
            if (_arrowPulseRoutine == null)
                _arrowPulseRoutine = StartCoroutine(CoArrowPulse());
        }
        else
        {
            if (_arrowPulseRoutine != null)
            {
                StopCoroutine(_arrowPulseRoutine);
                _arrowPulseRoutine = null;
            }

            anchorArrowVisual.transform.localScale = _arrowBaseScale;
        }
    }

    IEnumerator CoArrowPulse()
    {
        Transform tr = anchorArrowVisual.transform;

        while (true)
        {
            float duration = Mathf.Max(0.01f, arrowPulseDuration);
            float half = duration * 0.5f;

            // 1.0 -> max
            float t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / half);
                float eased = Mathf.SmoothStep(0f, 1f, k);
                float scale = Mathf.Lerp(1f, arrowPulseMaxScale, eased);
                tr.localScale = _arrowBaseScale * scale;
                yield return null;
            }

            // max -> 1.0
            t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / half);
                float eased = Mathf.SmoothStep(0f, 1f, k);
                float scale = Mathf.Lerp(arrowPulseMaxScale, 1f, eased);
                tr.localScale = _arrowBaseScale * scale;
                yield return null;
            }

            tr.localScale = _arrowBaseScale;
        }
    }

    /// <summary>
    /// defaultCap: 스테이지/툴의 기본 maxRailsPerNode
    /// </summary>
    public int GetCapacity(int defaultCap)
    {
        if (capacityOverride > 0) return capacityOverride;
        return Mathf.Max(1, defaultCap);
    }

    // =============================
    // Persistent Id
    // =============================
    [SerializeField] string persistentId;
    public string PersistentId => persistentId;

    public void EnsurePersistentId()
    {
        if (string.IsNullOrEmpty(persistentId))
            persistentId = Guid.NewGuid().ToString("N");
    }

    public void SetPersistentId(string id)
    {
        persistentId = id;
    }

    public Vector2 WorldPos => transform.position;

    void Awake()
    {
        EnsurePersistentId();

        if (anchorArrowVisual != null)
            _arrowBaseScale = anchorArrowVisual.transform.localScale;

        RefreshAnchorArrow();
    }

    void OnEnable()
    {
        if (anchorArrowVisual != null)
            _arrowBaseScale = anchorArrowVisual.transform.localScale;

        RefreshAnchorArrow();
    }

    void OnDisable()
    {
        if (_arrowPulseRoutine != null)
        {
            StopCoroutine(_arrowPulseRoutine);
            _arrowPulseRoutine = null;
        }

        if (anchorArrowVisual != null)
            anchorArrowVisual.transform.localScale = _arrowBaseScale;
    }

    void OnDestroy()
    {
        if (Application.isPlaying && RailSnapNodeManager.Instance != null)
            RailSnapNodeManager.Instance.Unregister(this);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying && anchorArrowVisual != null)
            _arrowBaseScale = anchorArrowVisual.transform.localScale;

        RefreshAnchorArrow();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 1f);
        Gizmos.DrawWireSphere(transform.position, 0.15f);
    }
#endif

    public void RebuildConnectedRailsFromScene()
    {
        _connectedRails.Clear();

        var rails = FindObjectsByType<RailSpan2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < rails.Length; i++)
        {
            var rail = rails[i];
            if (rail == null) continue;

            if (rail.startNode == this || rail.endNode == this)
                _connectedRails.Add(rail);
        }
    }

    public void RefreshVisualState()
    {
        RefreshAnchorArrow();
    }
}