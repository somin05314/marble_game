using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RailNodeFollowBinding2D : MonoBehaviour
{
    [Serializable]
    public struct Entry
    {
        public RailSnapNode2D node;
        public Transform anchorPoint;
        public Vector2 localOffset;
        public int ownerId;
        public string nodeId;
    }

    public int builtRevision = 0;
    public float builtRadius = -1f;
    public int builtMaskValue = 0;

    public int boundGraphRev { get => builtRevision; set => builtRevision = value; }
    public float boundRadius { get => builtRadius; set => builtRadius = value; }
    public int boundMaskValue { get => builtMaskValue; set => builtMaskValue = value; }

    public RailSnapNode2D node;
    public Transform anchorPoint;

    [SerializeField] List<Entry> entries = new List<Entry>(4);
    public IReadOnlyList<Entry> Entries => entries;

    public void Clear()
    {
        entries?.Clear();
        node = null;
        anchorPoint = null;

        builtRevision = 0;
        builtRadius = -1f;
        builtMaskValue = 0;
    }

    public void SetEntries(List<Entry> newEntries)
    {
        if (entries == null) entries = new List<Entry>(4);
        entries.Clear();

        if (newEntries != null && newEntries.Count > 0)
            entries.AddRange(newEntries);

        SyncCompatFieldsFromEntries();
    }

    void SyncCompatFieldsFromEntries()
    {
        if (entries != null && entries.Count > 0)
        {
            node = entries[0].node;
            anchorPoint = entries[0].anchorPoint;
        }
        else
        {
            node = null;
            anchorPoint = null;
        }
    }

    public bool ContainsNode(RailSnapNode2D n)
    {
        if (n == null || entries == null) return false;

        for (int i = 0; i < entries.Count; i++)
            if (entries[i].node == n) return true;

        return false;
    }

    public void RebuildLocalOffsetsFromCurrent()
    {
        BakeLocalOffsetsFromCurrent();
    }

    public void BakeLocalOffsetsFromCurrent()
    {
        if (entries == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];

            if (e.node != null && string.IsNullOrEmpty(e.nodeId))
            {
                e.node.EnsurePersistentId();
                e.nodeId = e.node.PersistentId;
            }

            if (e.anchorPoint != null)
                e.localOffset = (Vector2)transform.InverseTransformPoint(e.anchorPoint.position);
            else if (e.node != null)
                e.localOffset = (Vector2)transform.InverseTransformPoint(e.node.transform.position);
            else
                e.localOffset = Vector2.zero;

            entries[i] = e;
        }

        SyncCompatFieldsFromEntries();
    }

    public void SyncNow(bool syncPhysics, bool broadcastMoved = false)
    {
        if (entries == null || entries.Count == 0)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.node == null) continue;

            if (e.anchorPoint != null && !e.anchorPoint.gameObject.activeInHierarchy)
                continue;

            Vector3 targetWorld =
                e.anchorPoint != null
                    ? e.anchorPoint.position
                    : transform.TransformPoint(e.localOffset);

            targetWorld.z = 0f;

            Vector3 before = e.node.transform.position;
            if ((before - targetWorld).sqrMagnitude <= 0.0000001f)
                continue;

            var rb = e.node.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.position = (Vector2)targetWorld;
            else
                e.node.transform.position = targetWorld;

            if (broadcastMoved)
            {
                RailSpan2D.NotifyNodeMoved(e.node);
                RailNodeMoveBroadcaster2D.MarkMoved(e.node);
            }
        }

        if (syncPhysics)
            Physics2D.SyncTransforms();
    }

    void OnDestroy()
    {
        if (!Application.isPlaying) return;
        if (StageSaveManager.IsRestoringNow) return;
        if (entries == null || entries.Count == 0) return;

        var po = GetComponent<PlacementObject>();
        if (po == null) return;

        RailNodeSnapBinder.Detach(po);
    }

    [Serializable]
    public struct SnapshotEntry
    {
        public string nodeId;
        public string anchorPath;
        public Vector2 localOffset;
        public int ownerId;
    }

    [Serializable]
    public struct Snapshot
    {
        public int builtRevision;
        public float builtRadius;
        public int builtMaskValue;
        public List<SnapshotEntry> entries;
    }

    static string GetPath(Transform root, Transform target)
    {
        if (root == null) return null;
        if (target == null || target == root) return "";

        var stack = new Stack<string>();
        var t = target;

        while (t != null && t != root)
        {
            stack.Push(t.name);
            t = t.parent;
        }

        if (t != root) return null;
        return string.Join("/", stack);
    }

    RailSnapNode2D FindNodeById(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return null;

        var mgr = RailSnapNodeManager.Instance;
        if (mgr != null)
        {
            var found = mgr.FindById(nodeId);
            if (found != null) return found;
        }

        var all = FindObjectsOfType<RailSnapNode2D>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var n = all[i];
            if (n == null) continue;
            if (n.PersistentId == nodeId) return n;
        }

        return null;
    }

    public Snapshot CreateSnapshot()
    {
        BakeLocalOffsetsFromCurrent();

        var s = new Snapshot
        {
            builtRevision = builtRevision,
            builtRadius = builtRadius,
            builtMaskValue = builtMaskValue,
            entries = new List<SnapshotEntry>(entries != null ? entries.Count : 0)
        };

        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];

                string nodeId = e.nodeId;
                if (string.IsNullOrEmpty(nodeId) && e.node != null)
                {
                    e.node.EnsurePersistentId();
                    nodeId = e.node.PersistentId;
                }

                s.entries.Add(new SnapshotEntry
                {
                    nodeId = nodeId,
                    anchorPath = GetPath(transform, e.anchorPoint),
                    localOffset = e.localOffset,
                    ownerId = e.ownerId
                });
            }
        }

        return s;
    }

    public void RestoreSnapshot(in Snapshot s)
    {
        builtRevision = s.builtRevision;
        builtRadius = s.builtRadius;
        builtMaskValue = s.builtMaskValue;

        var newEntries = new List<Entry>(s.entries != null ? s.entries.Count : 0);

        if (s.entries != null)
        {
            for (int i = 0; i < s.entries.Count; i++)
            {
                var se = s.entries[i];

                var resolvedNode = FindNodeById(se.nodeId);
                Transform resolvedAnchor =
                    string.IsNullOrEmpty(se.anchorPath) ? transform : transform.Find(se.anchorPath);

                if (resolvedAnchor == null)
                    resolvedAnchor = transform;

                newEntries.Add(new Entry
                {
                    node = resolvedNode,
                    anchorPoint = resolvedAnchor,
                    localOffset = se.localOffset,
                    ownerId = se.ownerId,
                    nodeId = se.nodeId
                });
            }
        }

        SetEntries(newEntries);
    }

    public bool CleanupInvalidEntriesAndHasAnyBound()
    {
        if (entries == null || entries.Count == 0)
        {
            node = null;
            anchorPoint = null;
            return false;
        }

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];

            bool invalid =
                e.anchorPoint == null ||
                e.node == null ||
                !e.anchorPoint.gameObject.activeInHierarchy;

            if (invalid)
                entries.RemoveAt(i);
        }

        SyncCompatFieldsFromEntries();
        return entries.Count > 0;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        SyncCompatFieldsFromEntries();
    }
#endif
}