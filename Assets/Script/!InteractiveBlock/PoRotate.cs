using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class PoRotate : MonoBehaviour
{
    [Tooltip("비워두면 자기 자신(transform)을 회전")]
    [SerializeField] Transform target;

    [Tooltip("회전에 걸리는 시간(초)")]
    [SerializeField] float rotateDuration = 1f;

    [Header("Events (Optional)")]
    [Tooltip("회전 시작 시 호출")]
    public UnityEvent onRotateStart;

    [Tooltip("회전 종료 시 호출")]
    public UnityEvent onRotateComplete;

    [Header("Audio")]
    [Tooltip("회전 사운드를 담당하는 별도 오디오 플레이어")]
    [SerializeField] PoRotateAudioPlayer audioPlayer;

    public bool IsRotating => _isRotating;

    bool _isRotating = false;
    Coroutine _co;

    void Reset()
    {
        if (target == null)
            target = transform;

        if (audioPlayer == null)
            audioPlayer = GetComponentInChildren<PoRotateAudioPlayer>(true);
    }

    void Awake()
    {
        if (target == null)
            target = transform;
    }

    /// <summary>
    /// 현재 각도에서 deltaDegrees만큼 회전(상대 회전)
    /// </summary>
    public void RotateBy(float deltaDegrees)
    {
        if (_isRotating) return;

        float startZ = target.eulerAngles.z;
        float endZ = startZ + deltaDegrees;
        StartRotate(startZ, endZ);
    }

    /// <summary>
    /// absoluteDegrees(절대 각도)로 회전
    /// </summary>
    public void RotateTo(float absoluteDegrees)
    {
        if (_isRotating) return;

        float startZ = target.eulerAngles.z;
        float endZ = absoluteDegrees;
        StartRotate(startZ, endZ);
    }

    [ContextMenu("Rotate 90")]
    public void Rotate90()
    {
        RotateBy(90f);
    }

    void StartRotate(float startZ, float endZ)
    {
        if (_co != null)
            StopCoroutine(_co);

        _co = StartCoroutine(RotateRoutine(startZ, endZ));
    }

    IEnumerator RotateRoutine(float startZ, float endZ)
    {
        _isRotating = true;
        onRotateStart?.Invoke();

        if (audioPlayer != null)
        {
            audioPlayer.PlayRotateStart();
            audioPlayer.StartRotateLoop();
        }

        float elapsed = 0f;
        float delta = endZ - startZ;

        while (elapsed < rotateDuration)
        {
            elapsed += Time.deltaTime;
            float t = (rotateDuration <= 0.0001f) ? 1f : Mathf.Clamp01(elapsed / rotateDuration);

            float z = startZ + delta * t;

            var e = target.eulerAngles;
            e.z = z;
            target.eulerAngles = e;

            yield return null;
        }

        var final = target.eulerAngles;
        final.z = startZ + delta;
        target.eulerAngles = final;

        if (audioPlayer != null)
        {
            audioPlayer.StopRotateLoop();
            audioPlayer.PlayRotateEnd();
        }

        _isRotating = false;
        onRotateComplete?.Invoke();
        _co = null;
    }

    public void CancelRotate()
    {
        if (_co != null)
        {
            StopCoroutine(_co);
            _co = null;
        }

        if (audioPlayer != null)
            audioPlayer.StopAllRotateAudio();

        _isRotating = false;
    }

    public void SetZImmediate(float zDegrees)
    {
        CancelRotate();

        var e = target.eulerAngles;
        e.z = zDegrees;
        target.eulerAngles = e;
    }

    public void SetLocalRotationImmediate(Quaternion localRot)
    {
        CancelRotate();
        target.localRotation = localRot;
    }

    public void RotateToShortest(float absoluteDegrees)
    {
        if (_isRotating) return;

        float startZ = target.eulerAngles.z;
        float shortestDelta = Mathf.DeltaAngle(startZ, absoluteDegrees);
        float endZ = startZ + shortestDelta;

        StartRotate(startZ, endZ);
    }
}