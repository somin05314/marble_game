using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoalZoneRotate : MonoBehaviour
{
    [Header("Rotate Motors (2 targets)")]
    [SerializeField] PoRotate rotA;
    [SerializeField] PoRotate rotB;

    [Header("Rotate Amount (degrees)")]
    [Tooltip("정방향(Flip 안된 상태)에서 A가 회전해야 하는 각도(상대 회전)")]
    [SerializeField] float openDeltaA = 90f;

    [Tooltip("정방향(Flip 안된 상태)에서 B가 회전해야 하는 각도(상대 회전)")]
    [SerializeField] float openDeltaB = -90f;

    [Header("Flip Handling")]
    [Tooltip("Flip 판정 기준 Transform. 비워두면 자기 자신(transform)")]
    [SerializeField] Transform flipProbe;

    [Tooltip("Flip되었을 때 회전 방향(+/-)을 반전합니다.")]
    [SerializeField] bool invertWhenFlipped = true;

    [Header("State")]
    [SerializeField] bool startOpened = false;

    bool _opened;

    // ✅ 초기 상태 캐시
    Quaternion _rotA0;
    Quaternion _rotB0;
    bool _cached;

    void Awake()
    {
        if (flipProbe == null) flipProbe = transform;

        CacheInitialIfNeeded();
        ApplyInitialState(); // ✅ 시작 상태 반영
    }

    void CacheInitialIfNeeded()
    {
        if (_cached) return;
        if (rotA != null) _rotA0 = rotA.transform.localRotation;
        if (rotB != null) _rotB0 = rotB.transform.localRotation;
        _cached = true;
    }

    void ApplyInitialState()
    {
        // startOpened를 “초기 상태”로 강제
        ResetToInitial();
    }

    // ✅ 외부(리셋)에서 호출할 함수
    public void ResetToInitial()
    {
        CacheInitialIfNeeded();

        if (rotA != null) rotA.SetLocalRotationImmediate(_rotA0);
        if (rotB != null) rotB.SetLocalRotationImmediate(_rotB0);

        _opened = startOpened;
    }

    // =========================
    // ✅ UnityEvent에서 호출
    // =========================

    public void Open()
    {
        if (_opened) return;
        ApplyDelta(openDeltaA, openDeltaB);
        _opened = true;
    }

    public void Close()
    {
        if (!_opened) return;
        // Open의 반대로 돌리면 닫힘
        ApplyDelta(-openDeltaA, -openDeltaB);
        _opened = false;
    }

    public void Toggle()
    {
        if (_opened) Close();
        else Open();
    }

    // =========================
    // 내부
    // =========================

    void ApplyDelta(float deltaA, float deltaB)
    {
        bool flipped = IsFlipped();
        if (invertWhenFlipped && flipped)
        {
            deltaA = -deltaA;
            deltaB = -deltaB;
        }

        // rotA/rotB는 동시에 실행
        if (rotA != null) rotA.RotateBy(deltaA);
        if (rotB != null) rotB.RotateBy(deltaB);
    }

    bool IsFlipped()
    {
        // ✅ 가장 흔한 Flip 방식: localScale.x < 0
        // (너 프로젝트에서 flip을 scale로 처리하면 이게 정답)
        return flipProbe != null && flipProbe.lossyScale.x < 0f;
    }
}
