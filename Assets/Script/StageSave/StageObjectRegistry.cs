using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 내 저장 대상 오브젝트를 추적하는 레지스트리.
/// - PlacementObject
/// - RailSpan2D
///
/// 목적:
/// 저장/복원/스냅샷 시 FindObjectsOfType 남발을 줄이기 위한 기반.
/// </summary>
public class StageObjectRegistry : MonoBehaviour
{
    public static StageObjectRegistry Instance { get; private set; }

    readonly List<PlacementObject> _placementObjects = new List<PlacementObject>(128);
    readonly List<RailSpan2D> _rails = new List<RailSpan2D>(256);

    /// <summary>읽기 전용 조회용</summary>
    public IReadOnlyList<PlacementObject> PlacementObjects => _placementObjects;
    public IReadOnlyList<RailSpan2D> Rails => _rails;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            var reg = StageObjectRegistry.Instance;
            if (reg == null)
            {
                Debug.Log("Registry 없음");
                return;
            }

            reg.CleanupNulls();
            Debug.Log($"Registry -> PO: {reg.PlacementObjects.Count}, Rail: {reg.Rails.Count}");
        }
    }

    #region PlacementObject

    public void RegisterPO(PlacementObject po)
    {
        if (po == null) return;
        if (_placementObjects.Contains(po)) return;

        _placementObjects.Add(po);
    }

    public void UnregisterPO(PlacementObject po)
    {
        if (po == null) return;
        _placementObjects.Remove(po);
    }

    #endregion

    #region RailSpan2D

    public void RegisterRail(RailSpan2D rail)
    {
        if (rail == null) return;
        if (_rails.Contains(rail)) return;

        _rails.Add(rail);
    }

    public void UnregisterRail(RailSpan2D rail)
    {
        if (rail == null) return;
        _rails.Remove(rail);
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Destroy된 null 참조 정리
    /// 저장 직전에 한 번씩 호출해주면 안전함.
    /// </summary>
    public void CleanupNulls()
    {
        _placementObjects.RemoveAll(x => x == null);
        _rails.RemoveAll(x => x == null);
    }

    /// <summary>
    /// 혹시 등록 누락이 있었을 때 수동 재스캔용.
    /// 평소 저장 로직에서는 자주 쓰지 않는 게 목적.
    /// </summary>
    public void RebuildFromScene()
    {
        _placementObjects.Clear();
        _rails.Clear();

        var allPOs = FindObjectsOfType<PlacementObject>();
        for (int i = 0; i < allPOs.Length; i++)
        {
            var po = allPOs[i];
            if (po == null) continue;
            _placementObjects.Add(po);
        }

        var allRails = FindObjectsOfType<RailSpan2D>();
        for (int i = 0; i < allRails.Length; i++)
        {
            var rail = allRails[i];
            if (rail == null) continue;
            _rails.Add(rail);
        }
    }

    #endregion

    #region Static Helpers

    public static void Register(PlacementObject po)
    {
        Instance?.RegisterPO(po);
    }

    public static void Unregister(PlacementObject po)
    {
        Instance?.UnregisterPO(po);
    }

    public static void Register(RailSpan2D rail)
    {
        Instance?.RegisterRail(rail);
    }

    public static void Unregister(RailSpan2D rail)
    {
        Instance?.UnregisterRail(rail);
    }

    #endregion
}