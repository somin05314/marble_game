using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class PoMove : MonoBehaviour
{
    public enum MoveMode
    {
        Transform,
        Rigidbody2D
    }

    [Tooltip("비워두면 자기 자신(transform)을 이동")]
    [SerializeField] Transform target;

    [Header("Physics Move")]
    [SerializeField] MoveMode moveMode = MoveMode.Transform;

    [Tooltip("물리 이동에 사용할 Rigidbody2D. 비워두면 target/자기 자신에서 자동 탐색")]
    [SerializeField] Rigidbody2D targetRb2D;

    [Tooltip("체크하면 localPosition 기준으로 이동/저장")]
    [SerializeField] bool useLocalSpace = true;

    [Tooltip("이동에 걸리는 시간(초)")]
    [SerializeField] float moveDuration = 1f;

    [Header("Events (Optional)")]
    public UnityEvent onMoveStart;
    public UnityEvent onMoveComplete;

    [Header("Audio")]
    [SerializeField] PoMoveAudioPlayer audioPlayer;

    public bool IsMoving => _isMoving;
    public float MoveDuration => moveDuration;
    public Transform Target => target != null ? target : transform;
    public Rigidbody2D TargetRb2D => targetRb2D;

    bool _isMoving = false;
    Coroutine _co;

    void Reset()
    {
        if (target == null)
            target = transform;

        if (audioPlayer == null)
            audioPlayer = GetComponentInChildren<PoMoveAudioPlayer>(true);

        if (targetRb2D == null)
            targetRb2D = Target.GetComponent<Rigidbody2D>();
    }

    void Awake()
    {
        if (target == null)
            target = transform;

        if (targetRb2D == null)
            targetRb2D = Target.GetComponent<Rigidbody2D>();
    }

    Vector3 GetCurrentPosition()
    {
        if (moveMode == MoveMode.Rigidbody2D && targetRb2D != null)
        {
            Vector2 world = targetRb2D.position;
            return useLocalSpace ? WorldToStored(world) : (Vector3)world;
        }

        return useLocalSpace ? Target.localPosition : Target.position;
    }

    void SetCurrentPosition(Vector3 value)
    {
        if (moveMode == MoveMode.Rigidbody2D && targetRb2D != null)
        {
            Vector2 world = StoredToWorld(value);

            // Rigidbody 위치 즉시 반영
            targetRb2D.position = world;
            targetRb2D.velocity = Vector2.zero;
            targetRb2D.angularVelocity = 0f;

            // ✅ 즉시 복귀 안정화:
            // Transform도 같이 맞춰서 시각 위치가 한 박자 늦지 않게 함
            if (useLocalSpace)
            {
                Transform parent = Target.parent;
                if (parent != null)
                    Target.localPosition = value;
                else
                    Target.position = new Vector3(world.x, world.y, Target.position.z);
            }
            else
            {
                Target.position = new Vector3(world.x, world.y, Target.position.z);
            }

            return;
        }

        if (useLocalSpace) Target.localPosition = value;
        else Target.position = value;
    }

    Vector2 StoredToWorld(Vector3 stored)
    {
        if (!useLocalSpace)
            return new Vector2(stored.x, stored.y);

        Transform parent = Target.parent;
        if (parent == null)
            return new Vector2(stored.x, stored.y);

        Vector3 world = parent.TransformPoint(stored);
        return new Vector2(world.x, world.y);
    }

    Vector3 WorldToStored(Vector2 world)
    {
        if (!useLocalSpace)
            return new Vector3(world.x, world.y, Target.position.z);

        Transform parent = Target.parent;
        if (parent == null)
            return new Vector3(world.x, world.y, Target.localPosition.z);

        Vector3 local = parent.InverseTransformPoint(world);
        return new Vector3(local.x, local.y, Target.localPosition.z);
    }

    public void MoveBy(Vector3 delta, bool interrupt = false)
    {
        if (_isMoving && !interrupt) return;

        Vector3 start = GetCurrentPosition();
        Vector3 end = start + delta;
        StartMove(start, end);
    }

    public void MoveTo(Vector3 pos, bool interrupt = false)
    {
        if (_isMoving && !interrupt) return;

        Vector3 start = GetCurrentPosition();
        Vector3 end = pos;
        StartMove(start, end);
    }

    public void MoveByX(float dx) => MoveBy(new Vector3(dx, 0f, 0f));
    public void MoveByY(float dy) => MoveBy(new Vector3(0f, dy, 0f));
    public void MoveByZ(float dz) => MoveBy(new Vector3(0f, 0f, dz));

    public void MoveToX(float x)
    {
        if (_isMoving) return;

        Vector3 start = GetCurrentPosition();
        Vector3 end = new Vector3(x, start.y, start.z);
        StartMove(start, end);
    }

    public void MoveToY(float y)
    {
        if (_isMoving) return;

        Vector3 start = GetCurrentPosition();
        Vector3 end = new Vector3(start.x, y, start.z);
        StartMove(start, end);
    }

    public void MoveToZ(float z)
    {
        if (_isMoving) return;

        Vector3 start = GetCurrentPosition();
        Vector3 end = new Vector3(start.x, start.y, z);
        StartMove(start, end);
    }

    void StartMove(Vector3 start, Vector3 end)
    {
        if (_co != null)
            StopCoroutine(_co);

        _co = StartCoroutine(MoveRoutine(start, end));
    }

    IEnumerator MoveRoutine(Vector3 start, Vector3 end)
    {
        _isMoving = true;
        onMoveStart?.Invoke();

        if (audioPlayer != null)
        {
            audioPlayer.PlayMoveStart();
            audioPlayer.StartMoveLoop();
        }

        bool usePhysics = (moveMode == MoveMode.Rigidbody2D && targetRb2D != null);

        if (usePhysics)
        {
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.0001f, moveDuration);

            while (elapsed < safeDuration)
            {
                yield return new WaitForFixedUpdate();

                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);

                Vector3 stored = Vector3.Lerp(start, end, t);
                Vector2 world = StoredToWorld(stored);

                targetRb2D.MovePosition(world);
            }

            targetRb2D.MovePosition(StoredToWorld(end));
        }
        else
        {
            float elapsed = 0f;

            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;
                float t = (moveDuration <= 0.0001f) ? 1f : Mathf.Clamp01(elapsed / moveDuration);

                Vector3 p = Vector3.Lerp(start, end, t);
                SetCurrentPosition(p);

                yield return null;
            }

            SetCurrentPosition(end);
        }

        Physics2D.SyncTransforms();

        if (audioPlayer != null)
        {
            audioPlayer.StopMoveLoop();
            audioPlayer.PlayMoveEnd();
        }

        _isMoving = false;
        onMoveComplete?.Invoke();
        _co = null;
    }

    public void CancelMove()
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }

        if (targetRb2D != null)
        {
            targetRb2D.velocity = Vector2.zero;
            targetRb2D.angularVelocity = 0f;
            targetRb2D.Sleep();
        }

        Physics2D.SyncTransforms();

        if (audioPlayer != null)
            audioPlayer.StopAllMoveAudio();

        _isMoving = false;
    }

    public void SetPositionImmediate(Vector3 pos)
    {
        CancelMove();
        SetCurrentPosition(pos);
        Physics2D.SyncTransforms();
    }

    public void SetXImmediate(float x)
    {
        CancelMove();
        Vector3 p = GetCurrentPosition();
        p.x = x;
        SetCurrentPosition(p);
        Physics2D.SyncTransforms();
    }

    public void SetYImmediate(float y)
    {
        CancelMove();
        Vector3 p = GetCurrentPosition();
        p.y = y;
        SetCurrentPosition(p);
        Physics2D.SyncTransforms();
    }

    public void SetZImmediate(float z)
    {
        CancelMove();
        Vector3 p = GetCurrentPosition();
        p.z = z;
        SetCurrentPosition(p);
        Physics2D.SyncTransforms();
    }

    public Vector3 GetStoredPosition()
    {
        return GetCurrentPosition();
    }

    public void SetMoveDuration(float duration)
    {
        moveDuration = Mathf.Max(0f, duration);
    }
}