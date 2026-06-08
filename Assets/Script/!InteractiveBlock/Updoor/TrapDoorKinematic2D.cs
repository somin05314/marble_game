using System.Collections;
using UnityEngine;

public class TrapDoorKinematic2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Rigidbody2D doorRb;     // Kinematic
    [SerializeField] Transform pivot;        // 회전축 Transform

    [Header("Angles (relative to closed)")]
    [Tooltip("발판 밟자마자 8시로 살짝 내려가는 각도(보통 음수)")]
    [SerializeField] float dipDeltaDeg = -15f;

    [Tooltip("12시까지 올라가는 각도(보통 양수)")]
    [SerializeField] float openDeltaDeg = +90f;

    [Header("Timing")]
    [SerializeField] float dipTime = 0.12f;
    [SerializeField] float WaitTime = 0.12f;
    [SerializeField] float openTime = 0.35f;

    [Header("Acceleration Curve (0~1)")]
    [SerializeField] AnimationCurve accelCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Options")]
    [SerializeField] bool preventRetriggerWhileRunning = true;

    float _closedRotDeg;
    Vector2 _closedOffset; // pivot -> door 위치 오프셋(닫힘 기준)
    bool _running;

    void Reset()
    {
        doorRb = GetComponentInChildren<Rigidbody2D>();
    }

    void Awake()
    {
        if (doorRb == null) doorRb = GetComponent<Rigidbody2D>();

        // Door는 Kinematic 권장
        if (doorRb != null)
            doorRb.bodyType = RigidbodyType2D.Kinematic;

        // “현재 상태”를 닫힘(기준)으로 저장
        _closedRotDeg = doorRb.rotation;
        _closedOffset = (Vector2)doorRb.position - (Vector2)pivot.position;
    }

    public void TriggerOpen()
    {
        if (preventRetriggerWhileRunning && _running) return;
        StopAllCoroutines();
        StartCoroutine(Co_Sequence());
    }

    IEnumerator Co_Sequence()
    {
        _running = true;

        float dipTarget = _closedRotDeg + dipDeltaDeg;
        float openTarget = _closedRotDeg + openDeltaDeg;

        // 1) Dip (8시로 살짝 내려가기)
        yield return RotateTo(dipTarget, dipTime, ease01: null);

        // ✅ 1초 쉬었다가
        yield return new WaitForSeconds(WaitTime);

        // 2) Open (가속해서 12시로)
        yield return RotateTo(openTarget, openTime, ease01: accelCurve);

        _running = false;
    }

    IEnumerator RotateTo(float targetDeg, float duration, AnimationCurve ease01)
    {
        float startDeg = doorRb.rotation;
        float t = 0f;

        while (t < duration)
        {
            t += Time.fixedDeltaTime;
            float u = Mathf.Clamp01(t / duration);

            // 가속 느낌: 커브가 있으면 커브값 사용
            float k = (ease01 != null) ? ease01.Evaluate(u) : u;

            float curDeg = Mathf.LerpAngle(startDeg, targetDeg, k);
            ApplyRotationAboutPivot(curDeg);

            yield return new WaitForFixedUpdate();
        }

        ApplyRotationAboutPivot(targetDeg);
    }

    void ApplyRotationAboutPivot(float newDeg)
    {
        // 닫힘 기준으로 얼마나 회전했는지
        float delta = newDeg - _closedRotDeg;

        // pivot 기준 회전된 위치 계산
        Vector2 pivotPos = pivot.position;
        Vector2 rotatedOffset = Rotate(_closedOffset, delta);

        Vector2 newPos = pivotPos + rotatedOffset;

        // Rigidbody2D로 이동 (물리엔진에 “움직였음”을 알려줌)
        doorRb.MovePosition(newPos);
        doorRb.MoveRotation(newDeg);
    }

    static Vector2 Rotate(Vector2 v, float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad);
        float s = Mathf.Sin(rad);
        return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
    }
}
