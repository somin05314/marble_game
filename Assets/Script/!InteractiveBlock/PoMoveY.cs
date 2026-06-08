using System.Collections;
using UnityEngine;

public class PoMoveY: MonoBehaviour
{
    [Tooltip("비워두면 자기 자신(transform)을 이동")]
    [SerializeField] Transform target;

    [Tooltip("아래로 내릴 거리 (기본 4)")]
    [SerializeField] float dropDistanceY = 4f;

    [Tooltip("이동에 걸리는 시간(초)")]
    [SerializeField] float dropDuration = 1f;

    bool isDropping = false;

    void Awake()
    {
        if (target == null) target = transform;
    }

    // ✅ UnityEvent에서 호출 (1초 동안 y가 4 내려감)
    public void DropY4()
    {
        if (isDropping) return;
        StartCoroutine(DropRoutine());
    }

    IEnumerator DropRoutine()
    {
        isDropping = true;

        Vector3 start = target.position;
        Vector3 end = start + Vector3.down * dropDistanceY;

        float elapsed = 0f;

        while (elapsed < dropDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dropDuration);

            target.position = Vector3.Lerp(start, end, t);

            yield return null;
        }

        target.position = end;
        isDropping = false;
    }
}
