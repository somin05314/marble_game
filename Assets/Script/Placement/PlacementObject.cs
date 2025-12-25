using System.Collections.Generic;
using System;
using UnityEngine;

public enum PhysicsType
{
    Static,
    DynamicNoGravity,
    DynamicGravity
}

public class PlacementObject : MonoBehaviour
{
    [HideInInspector] public List<SnapConnection> connections = new();
    public PlacementData placementData;

    // 캐시
    Collider2D[] colliders;
    SpriteRenderer[] renderers;

    // ✅ Overlap 결과 배열 재사용 (할당 방지)
    readonly Collider2D[] overlapResults = new Collider2D[16];

    [Header("Physics")]
    public PhysicsType physicsType = PhysicsType.Static;


    [SerializeField, HideInInspector]
    string persistentId;

    public string PersistentId => persistentId;

    public void EnsureId()
    {
        if (string.IsNullOrEmpty(persistentId))
            persistentId = Guid.NewGuid().ToString();
    }

    public void SetPersistentId(string id) => persistentId = id;

    public bool HasMultipleConnections => connections.Count >= 2;

    void Awake()
    {
        EnsureId();
        colliders = GetComponentsInChildren<Collider2D>(true);
        renderers = GetComponentsInChildren<SpriteRenderer>(true);

        // SnapRoot/SnapPoint owner 연결 (필수 초기화)
        var roots = GetComponentsInChildren<SnapRoot>(true);
        foreach (var root in roots)
        {
            root.owner = this;
            foreach (var p in root.GetComponentsInChildren<SnapPoint>(true))
                p.root = root;
        }
    }

    // =========================
    // Snap
    // =========================
    public void BreakAllSnaps()
    {
        var snapshot = new List<SnapConnection>(connections);

        foreach (var c in snapshot)
        {
            if (c.otherRoot != null && c.otherRoot.owner != null)
            {
                c.otherRoot.owner.connections.RemoveAll(
                    x => x.otherRoot == c.myRoot
                );
            }
        }

        connections.Clear();
    }

    // placedLayer 제거(안 쓰이니까)
  
    // =========================
    // Modes
    // =========================
    public void SetGhost()
    {
        SetLayerRecursively(gameObject, LayerMask.NameToLayer("Ghost"));

        foreach (var col in colliders)
        {
            if (col == null) continue;
            col.enabled = true;
            col.isTrigger = true;
        }

        SetGhostVisual(true);
    }

    public void SetPlaced()
    {
        SetLayerRecursively(gameObject, LayerMask.NameToLayer("PlacedObject"));

        foreach (var col in colliders)
        {
            if (col == null) continue;
            col.enabled = true;
            col.isTrigger = false;
        }

        // Static이면 velocity/angVel 건드리면 경고 나니까 방어
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null && rb.bodyType != RigidbodyType2D.Static)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        SetNormalVisual();
    }

    // =========================
    // Visual
    // =========================
    public void SetGhostVisual(bool canPlace)
    {
        Color c = canPlace ? Color.green : Color.red;

        foreach (var sr in renderers)
        {
            if (sr == null) continue;
            sr.color = new Color(c.r, c.g, c.b, 0.4f);
        }
    }

    public void SetNormalVisual()
    {
        foreach (var sr in renderers)
        {
            if (sr == null) continue;
            sr.color = Color.white;
        }
    }

    public void SetSelectedVisual()
    {
        foreach (var sr in renderers)
        {
            if (sr == null) continue;
            sr.color = new Color(1f, 1f, 0.6f, 1f);
        }
    }

    public void SetPlacedVisual() => SetNormalVisual();

    static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    public bool CanPlaceByRule(
    LayerMask wallLayer,
    PlacementObject snapTarget,
    float allowedSnapPenetration = 0f
)
    {
        ContactFilter2D filter = new();
        filter.useTriggers = true; // ✅ Ghost(Trigger) 상태에서도 Overlap이 잡히게

        for (int c = 0; c < colliders.Length; c++)
        {
            var col = colliders[c];
            if (col == null || !col.enabled) continue;

            int count = col.OverlapCollider(filter, overlapResults);

            for (int i = 0; i < count; i++)
            {
                var hit = overlapResults[i];
                if (hit == null) continue;

                // 자기 자신 무시
                if (hit.transform.IsChildOf(transform))
                    continue;

                int hitLayer = hit.gameObject.layer;

                // 벽은 무조건 불가
                if (((1 << hitLayer) & wallLayer) != 0)
                    return false;

                // PlacementObject 아닌 건(예: Marble) 일단 무시
                var other = hit.GetComponentInParent<PlacementObject>();
                if (other == null) continue;

                // 스냅 대상은 "침투 허용치"까지만 허용
                if (snapTarget != null && other == snapTarget)
                {
                    var d = col.Distance(hit);
                    if (d.isOverlapped)
                    {
                        float penetration = -d.distance; // 겹친 깊이
                        if (penetration > allowedSnapPenetration)
                            return false;
                    }
                    continue;
                }

                // 그 외 PlacementObject와 겹치면 불가
                return false;
            }
        }

        return true;
    }


}
