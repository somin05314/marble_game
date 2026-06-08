using System.Collections.Generic;
using UnityEngine;

public class MoveHintBroker2D : MonoBehaviour
{
    public static MoveHintBroker2D Instance { get; private set; }

    [SerializeField] MoveHintOverlay2D overlay;

    struct Req
    {
        public int priority;
        public List<Vector2> pts;
    }

    readonly Dictionary<object, Req> _reqs = new();
    readonly List<object> _keys = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Request(object key, int priority, IReadOnlyList<Vector2> points)
    {
        if (overlay == null || key == null) return;

        if (!_reqs.TryGetValue(key, out var r))
        {
            r = new Req { pts = new List<Vector2>(256) };
            _reqs[key] = r;
        }

        r.priority = priority;
        r.pts.Clear();

        if (points != null)
        {
            for (int i = 0; i < points.Count; i++)
                r.pts.Add(points[i]);
        }

        _reqs[key] = r;
    }

    public void Clear(object key)
    {
        if (key == null) return;
        _reqs.Remove(key);
    }

    void LateUpdate()
    {
        if (overlay == null)
            return;

        // 이번 프레임에 들어온 요청 중 우선순위 가장 높은 것 선택
        int bestPri = int.MinValue;
        Req best = default;
        bool hasBest = false;

        _keys.Clear();
        foreach (var kv in _reqs) _keys.Add(kv.Key);

        // 오래된 요청(다음 프레임까지 갱신 안 된 것)은 자동 제거
        for (int i = _keys.Count - 1; i >= 0; i--)
        {
            var k = _keys[i];
            var r = _reqs[k];

            if (!hasBest || r.priority > bestPri)
            {
                bestPri = r.priority;
                best = r;
                hasBest = true;
            }
        }

        if (!hasBest || best.pts == null || best.pts.Count == 0)
            overlay.HideAll();
        else
            overlay.ShowDots(best.pts);
    }
}
