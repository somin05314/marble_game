using UnityEngine;

[DisallowMultipleComponent]
public class RailNodeFollow2D : MonoBehaviour
{
    public Transform target;
    public bool IsFollowing => target != null;

    public int ownerId;

    RailSnapNode2D _node;
    Rigidbody2D _rb;
    Vector3 _lastPos;

    int _orphanFrames;
    const int ORPHAN_FRAMES_TO_SELF_DESTRUCT = 2;

    public bool runtimeFollowEnabled = false;
    void Awake()
    {
        _node = GetComponent<RailSnapNode2D>();
        _rb = GetComponent<Rigidbody2D>();
        _lastPos = transform.position;
    }

    void SetNodePos(Vector3 p)
    {
        p.z = 0f;

        if (_rb != null) _rb.position = (Vector2)p;
        else transform.position = p;

        _lastPos = p;

        Physics2D.SyncTransforms();

        if (_node != null)
        {
            RailSpan2D.NotifyNodeMoved(_node);

            // ✅ 추가: Manager 캐시/ID 갱신
            var mgr = RailSnapNodeManager.Instance;
            if (mgr != null) mgr.OnNodeMoved(_node);
        }
    }

    public void Attach(Transform t, int newOwnerId)
    {
        ownerId = newOwnerId;
        target = t;
        _orphanFrames = 0;

        if (target == null) return;
        SetNodePos(target.position);
    }

    public void Detach()
    {
        target = null;
        ownerId = 0;
        _orphanFrames = 0;
    }



    void LateUpdate()
    {
        if (StageSaveManager.IsRestoringNow)
            return;

        if (!runtimeFollowEnabled)
            return;

        if (target == null)
        {
            _orphanFrames++;
            if (_orphanFrames >= ORPHAN_FRAMES_TO_SELF_DESTRUCT)
                Destroy(this);
            return;
        }

        _orphanFrames = 0;

        var p = target.position;
        p.z = 0f;

        if ((p - _lastPos).sqrMagnitude < 0.0000005f)
            return;

        SetNodePos(p);
    }
}