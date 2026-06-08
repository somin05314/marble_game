using System.Collections.Generic;
using UnityEngine;

public class RailSnapNodeManager : MonoBehaviour
{
    public static RailSnapNodeManager Instance { get; private set; }

    [Header("Prefab")]
    public RailSnapNode2D nodePrefab;

    [Header("Mask (RailNode ONLY)")]
    public LayerMask railNodeMask;

    [Header("Merge (Near)")]
    public float mergeRadius = 0.15f;

    [Header("Merge (Exact)")]
    [Tooltip("같은 위치 판정용 양자화 단위. 그리드 스냅이면 0.001~0.01 정도 추천")]
    public float exactQuantize = 0.001f;

    static readonly Collider2D[] hits = new Collider2D[32];

    // ✅ “완전 동일 위치” 노드 캐시 (Follow 노드는 넣지 않음)
    readonly Dictionary<Vector2Int, RailSnapNode2D> exactNodeCache = new();

    // ✅ PersistentId 기반 추적
    readonly Dictionary<string, RailSnapNode2D> idToNode = new();
    readonly Dictionary<string, Vector2Int> idToKey = new();
    readonly Dictionary<Vector2Int, string> keyToId = new();

    int railNodeLayer = -1;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        railNodeLayer = LayerMask.NameToLayer("RailNode");
        if (railNodeLayer < 0)
            Debug.LogWarning("[RailNode] Layer 'RailNode' not found. Please create it in Unity layers.");

        // ✅ 씬에 있던 노드 등록
        RegisterSceneNodes();
    }

    void RegisterSceneNodes()
    {
#if UNITY_2022_2_OR_NEWER
        var nodes = FindObjectsByType<RailSnapNode2D>(FindObjectsSortMode.None);
#else
        var nodes = FindObjectsOfType<RailSnapNode2D>();
#endif
        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            if (n == null) continue;
            Register(n);
        }
    }

    // ---------------------------------------------------------
    // Reset helpers (Snapshot / Reset)
    // ---------------------------------------------------------
    /// <summary>
    /// 이 매니저(자식으로 생성된 런타임 노드들)를 정리합니다.
    /// - Snapshot Reset 시 '노드 누적'로 인한 렉을 방지하기 위해 사용
    /// - 씬에 배치된 고정 노드(앵커 등)는 건드리지 않습니다.
    /// </summary>
    public void DestroyRuntimeNodesUnderManager()
    {
        // 런타임 생성 노드는 모두 manager의 자식으로 생성되도록 설계됨
        var toDestroy = new List<GameObject>(transform.childCount);
        for (int i = 0; i < transform.childCount; i++)
        {
            var ch = transform.GetChild(i);
            if (ch == null) continue;
            if (ch.GetComponent<RailSnapNode2D>() == null) continue;
            toDestroy.Add(ch.gameObject);
        }

        for (int i = 0; i < toDestroy.Count; i++)
        {
            var go = toDestroy[i];
            if (go == null) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(go);
            else Destroy(go);
#else
            Destroy(go);
#endif
        }
    }

    /// <summary>내부 캐시(딕셔너리)를 전부 비웁니다.</summary>
    public void ClearAllCaches()
    {
        exactNodeCache.Clear();
        idToNode.Clear();
        idToKey.Clear();
        keyToId.Clear();
    }

    /// <summary>
    /// 씬에 존재하는 노드들을 다시 스캔하여 캐시를 재구성합니다.
    /// - 보통 Reset/Restore에서 Destroy 다음 프레임에 호출하는 것이 안전합니다.
    /// </summary>
    public void RebuildCachesFromScene()
    {
        ClearAllCaches();
        RegisterSceneNodes();
    }

    // ---------------------------------------------------------
    // PersistentId map API
    // ---------------------------------------------------------
    public void Register(RailSnapNode2D node)
    {
        if (node == null) return;

        node.EnsurePersistentId();

        string id = node.PersistentId;
        idToNode[id] = node;

        // Follow 노드는 exact 캐시에 넣지 않음
        if (node.GetComponent<RailNodeFollow2D>() != null)
            return;

        var key = MakeKey(node.transform.position);
        idToKey[id] = key;
        keyToId[key] = id;
        exactNodeCache[key] = node;
    }

    public RailSnapNode2D FindById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        if (idToNode.TryGetValue(id, out var node))
        {
            if (node == null)
            {
                idToNode.Remove(id);
                idToKey.Remove(id);
                return null;
            }
            return node;
        }
        return null;
    }

    public void OnNodeMoved(RailSnapNode2D node)
    {
        if (node == null) return;

        node.EnsurePersistentId();
        string id = node.PersistentId;

        // ✅ old key 제거는 Follow 여부 상관없이 수행
        if (idToKey.TryGetValue(id, out var oldKey))
        {
            if (keyToId.TryGetValue(oldKey, out var mapped) && mapped == id)
                keyToId.Remove(oldKey);

            if (exactNodeCache.TryGetValue(oldKey, out var cached) && cached == node)
                exactNodeCache.Remove(oldKey);
        }

        // ✅ Follow 노드는 "새 key 등록"은 하지 않음 (정리만 하고 끝)
        if (node.GetComponent<RailNodeFollow2D>() != null)
        {
            idToKey.Remove(id); // (선택) Follow면 idToKey도 비우는 게 더 안전
            return;
        }

        // new key 등록
        var newKey = MakeKey(node.transform.position);
        idToKey[id] = newKey;
        keyToId[newKey] = id;
        exactNodeCache[newKey] = node;
    }

    public void Unregister(RailSnapNode2D node)
    {
        if (node == null) return;

        node.EnsurePersistentId();
        string id = node.PersistentId;

        // idToNode 정리
        if (idToNode.TryGetValue(id, out var cur) && cur == node)
            idToNode.Remove(id);

        // key 매핑 정리
        if (idToKey.TryGetValue(id, out var key))
        {
            idToKey.Remove(id);

            if (keyToId.TryGetValue(key, out var mapped) && mapped == id)
                keyToId.Remove(key);

            if (exactNodeCache.TryGetValue(key, out var cached) && cached == node)
                exactNodeCache.Remove(key);
        }

        // exactNodeCache에서 node를 가리키는 모든 키 제거(보험)
        List<Vector2Int> removeKeys = null;
        foreach (var kv in exactNodeCache)
        {
            if (kv.Value == null || kv.Value == node)
            {
                removeKeys ??= new List<Vector2Int>();
                removeKeys.Add(kv.Key);
            }
        }
        if (removeKeys != null)
        {
            for (int i = 0; i < removeKeys.Count; i++)
                exactNodeCache.Remove(removeKeys[i]);
        }
    }

    // ---------------------------------------------------------
    // GetOrCreate
    // ---------------------------------------------------------
    public RailSnapNode2D GetOrCreate(Vector2 worldPos)
        => GetOrCreate(worldPos, out _);

    // ✅ (호환용) 예전 API 유지: asAnchorRoot 의미 없어져서 그냥 GetOrCreate로 처리
    public RailSnapNode2D GetOrCreate(Vector2 worldPos, bool asAnchorRoot)
        => GetOrCreate(worldPos);

    public RailSnapNode2D GetOrCreate(Vector2 worldPos, out bool createdNew)
    {
        createdNew = false;

        if (nodePrefab == null)
        {
            Debug.LogError("[RailNode] nodePrefab is NULL!");
            return null;
        }

        // 1) exact cache
        var key = MakeKey(worldPos);
        if (exactNodeCache.TryGetValue(key, out var cached))
        {
            if (cached == null)
            {
                exactNodeCache.Remove(key);
            }
            else
            {
                // Follow 컴포넌트가 있으면 exact 캐시에 남아 있으면 안 됨
                if (cached.GetComponent<RailNodeFollow2D>() == null)
                    return cached;

                exactNodeCache.Remove(key);
            }
        }

        // 2) near merge
        Physics2D.SyncTransforms();
        int count = Physics2D.OverlapCircleNonAlloc(worldPos, mergeRadius, hits, railNodeMask);

        RailSnapNode2D best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var col = hits[i];
            if (col == null) continue;

            var node = col.GetComponentInParent<RailSnapNode2D>();
            if (node == null) continue;

            // Follow 노드는 merge 후보 제외
            if (node.GetComponent<RailNodeFollow2D>() != null)
                continue;

            float d = ((Vector2)node.transform.position - worldPos).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = node;
            }
        }

        if (best != null)
        {
            Register(best);
            exactNodeCache[MakeKey(best.transform.position)] = best;
            exactNodeCache[key] = best;
            return best;
        }

        // 3) create new
        createdNew = true;
        return CreateFreeNodeInternal(worldPos);
    }

    public RailSnapNode2D CreateFreeNode(Vector2 worldPos)
    {
        var node = CreateFreeNodeInternal(worldPos);
        exactNodeCache[MakeKey(worldPos)] = node;
        return node;
    }

    public RailSnapNode2D CreateLooseNode(Vector2 worldPos) => CreateFreeNode(worldPos);

    RailSnapNode2D CreateFreeNodeInternal(Vector2 worldPos)
    {
        var created = Instantiate(nodePrefab, worldPos, Quaternion.identity, transform);
        created.name = $"RailNode_{worldPos.x:F3}_{worldPos.y:F3}";

        // ✅ 런타임 생성 노드: 고정 앵커 아님
        created.SetAnchor(false);

        // ✅ 런타임 생성 노드: 용량 2로 고정
        created.SetCapacityOverride(2);

        if (railNodeLayer >= 0)
            SetLayerRecursively(created.gameObject, railNodeLayer);

        Register(created);
        return created;
    }



    public RailSnapNode2D CreateFollowNode(Vector2 worldPos, Transform followTarget, int ownerId = 0)
    {
        var node = CreateFreeNodeInternal(worldPos);

        var follow = node.GetComponent<RailNodeFollow2D>();
        if (follow == null) follow = node.gameObject.AddComponent<RailNodeFollow2D>();
        follow.Attach(followTarget, ownerId);

        // Follow 노드는 exact 캐시 제거
        var key = MakeKey(worldPos);
        if (exactNodeCache.TryGetValue(key, out var cached) && cached == node)
            exactNodeCache.Remove(key);

        return node;
    }

    // ---------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------
    Vector2Int MakeKey(Vector2 worldPos)
    {
        float q = Mathf.Max(0.0001f, exactQuantize);
        return new Vector2Int(
            Mathf.RoundToInt(worldPos.x / q),
            Mathf.RoundToInt(worldPos.y / q)
        );
    }

    static void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;

        var t = obj.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
    }
}
