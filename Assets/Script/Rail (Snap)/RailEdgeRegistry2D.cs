using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬에 "확정된" 레일(start/end) 정보를 해시로 저장해
/// 힌트 생성/검사에서 O(1) 중복 판정을 하게 해주는 레지스트리.
/// </summary>
public static class RailEdgeRegistry2D
{
    const float EPS = 0.01f; // 중복 판정 정밀도(너무 작으면 오차, 너무 크면 다른 레일까지 중복)
    static readonly Dictionary<int, ulong> _ownerToKey = new(); // railInstanceId -> key
    static readonly HashSet<ulong> _keys = new();

    public static int DebugKeyCount => _keys.Count;

    public static bool DebugHasOwner(int railId) => _ownerToKey.ContainsKey(railId);

    public static bool DebugTryGetKey(int railId, out ulong key) => _ownerToKey.TryGetValue(railId, out key);

    static int Quant(float v) => Mathf.RoundToInt(v / EPS);

    static ulong Pack(int ax, int ay, int bx, int by)
    {
        unchecked
        {
            uint uax = (uint)(ax & 0xFFFF);
            uint uay = (uint)(ay & 0xFFFF);
            uint ubx = (uint)(bx & 0xFFFF);
            uint uby = (uint)(by & 0xFFFF);
            return ((ulong)uax << 48) | ((ulong)uay << 32) | ((ulong)ubx << 16) | (ulong)uby;
        }
    }

    public static ulong MakeKey(Vector2 a, Vector2 b)
    {
        int ax = Quant(a.x);
        int ay = Quant(a.y);
        int bx = Quant(b.x);
        int by = Quant(b.y);

        // 방향 무시(정렬)
        bool swap = (ax > bx) || (ax == bx && ay > by);
        if (swap)
        {
            (ax, bx) = (bx, ax);
            (ay, by) = (by, ay);
        }
        return Pack(ax, ay, bx, by);
    }

    public static bool Contains(Vector2 a, Vector2 b)
        => _keys.Contains(MakeKey(a, b));

    /// <summary>레일 확정 시 등록(이전 등록이 있으면 갱신)</summary>
    public static void Register(int railId, Vector2 start, Vector2 end)
    {
        Unregister(railId);
        ulong k = MakeKey(start, end);
        _ownerToKey[railId] = k;
        _keys.Add(k);
    }

    /// <summary>레일 삭제/드래그 시작 시 제거</summary>
    public static void Unregister(int railId)
    {
        if (_ownerToKey.TryGetValue(railId, out var k))
        {
            _ownerToKey.Remove(railId);
            _keys.Remove(k);
        }
    }

    /// <summary>디버그/리셋용</summary>
    public static void ClearAll()
    {
        _ownerToKey.Clear();
        _keys.Clear();
    }
}
