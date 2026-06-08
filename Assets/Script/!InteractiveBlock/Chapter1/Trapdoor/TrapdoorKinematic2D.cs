using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class TrapdoorKinematic2D : MonoBehaviour
{
    public Rigidbody2D rb;

    [Header("Angles")]
    public float closedAngle = 0f;      // 닫힘 각도(기준)
    public float openDelta = -90f;      // 닫힘에서 얼마나 열릴지(기본: 아래로 -90)

    [Header("Open Motion")]
    public float openTime = 0.6f;       // 전체 열림 시간(초)
    public float openDelay = 0.02f;
    public float easePower = 2f;        // (현재는 SmoothStep 사용) 필요하면 커스텀 easing에 활용

    bool opening;

    void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        // 항상 Kinematic
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;

        // 시작 각도 고정
        rb.rotation = closedAngle;
    }

    // ✅ FlipX 여부(스케일 x<0이면 플립된 상태로 간주)
    bool IsFlipX() => transform.lossyScale.x < 0f;

    // ✅ 플립 상태에 따라 열림 방향(회전 부호) 보정
    float GetTargetOpenAngle()
    {
        float sign = IsFlipX() ? -1f : 1f;
        return closedAngle + openDelta * sign;
    }

    public void Open()
    {
        if (opening) return;
        opening = true;
        StartCoroutine(OpenRoutine());
    }

    IEnumerator OpenRoutine()
    {
        if (openDelay > 0f)
            yield return new WaitForSeconds(openDelay);

        float start = rb.rotation;
        float end = GetTargetOpenAngle();

        float t = 0f;
        float invTime = 1f / Mathf.Max(0.0001f, openTime);

        while (t < 1f)
        {
            t += Time.fixedDeltaTime * invTime;

            // ✅ 가속+감속(EaseInOut)
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

            float angle = Mathf.LerpAngle(start, end, eased);
            rb.MoveRotation(angle);

            yield return new WaitForFixedUpdate();
        }

        rb.MoveRotation(end);
    }
}
