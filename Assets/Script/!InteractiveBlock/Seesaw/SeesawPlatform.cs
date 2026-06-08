using UnityEngine;
using System.Collections;

public class SeesawPlatform : MonoBehaviour
{
    [SerializeField] Transform target;

    [Header("Durations (per trigger)")]
    [SerializeField] float durMinus15 = 1f;
    [SerializeField] float durMinus7 = 1f;
    [SerializeField] float durPlus7 = 1f;
    [SerializeField] float durPlus15 = 1f;

    Coroutine _co;

    void Awake()
    {
        if (target == null) target = transform;
    }

    // -15 트리거: 무조건 -15도로
    public void SetMinus10()
    {
        RotateToAngle(-15f, durMinus15);
    }

    // -7 트리거: 현재가 -7보다 "높으면"(>-7)만 -7로
    public void SetMinus5_IfHigher()
    {
        float cur = GetCurrentSignedZ();
        if (cur > -7f) RotateToAngle(-7f, durMinus7);
    }

    // +7 트리거: 현재가 +7보다 "낮으면"(<+7)만 +7로
    public void SetPlus5_IfLower()
    {
        float cur = GetCurrentSignedZ();
        if (cur < 7f) RotateToAngle(7f, durPlus7);
    }

    // +15 트리거: 무조건 +15도로
    public void SetPlus10()
    {
        RotateToAngle(15f, durPlus15);
    }

    float GetCurrentSignedZ()
    {
        float z = target.eulerAngles.z;
        if (z > 180f) z -= 360f;
        return z;
    }

    void RotateToAngle(float endSignedZ, float duration)
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(RotateRoutine(endSignedZ, duration));
    }

    IEnumerator RotateRoutine(float endSignedZ, float duration)
    {
        float startSignedZ = GetCurrentSignedZ();
        float dur = Mathf.Max(0.0001f, duration);

        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);

            float z = Mathf.Lerp(startSignedZ, endSignedZ, t);

            var e = target.eulerAngles;
            e.z = WrapTo360(z);
            target.eulerAngles = e;

            yield return null;
        }

        var ee = target.eulerAngles;
        ee.z = WrapTo360(endSignedZ);
        target.eulerAngles = ee;

        _co = null;
    }

    float WrapTo360(float signedZ)
    {
        float z = signedZ % 360f;
        if (z < 0f) z += 360f;
        return z;
    }
}
