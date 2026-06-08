using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MarbleRollingAudio : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] AudioSource rollAudio;
    [SerializeField] AudioSource impactAudio;
    [SerializeField] Rigidbody2D rb;

    [Tooltip("자식의 Trigger 콜라이더가 붙은 오브젝트")]
    [SerializeField] MarbleRollTrigger2D rollTrigger;

    [Header("Rolling Clips")]
    [SerializeField] AudioClip[] rollingClips;

    [Header("Impact")]
    [SerializeField] AudioClip impactClip;
    [SerializeField, Range(0f, 1f)] float impactVolume = 0.5f;
    [SerializeField] float impactCooldown = 0.08f;

    [Header("Rolling Volume")]
    [SerializeField, Range(0f, 1f)] float rollingVolumeMultiplier = 1f;
    [SerializeField, Range(0f, 1f)] float rollingMaxVolume = 0.2f;

    [Header("Threshold")]
    [SerializeField] float minRollingSpeed = 0.15f;

    [Header("Layer")]
    [SerializeField] LayerMask rollingSurfaceMask;

    [Header("Restart Condition")]
    [Tooltip("접촉이 0인 상태가 이 시간 이상 유지된 뒤 다시 닿으면 롤링 사운드를 재시작")]
    [SerializeField] float restartAfterNoContactTime = 0.2f;

    [Header("Destroy")]
    [SerializeField] AudioClip destroyClip;
    [SerializeField, Range(0f, 1f)] float destroyVolume = 1f;

    readonly HashSet<Collider2D> _rollingSurfaceColliders = new HashSet<Collider2D>();

    float _pitchSeed = 1f;
    int _lastClipIndex = -1;
    float _lastImpactTime = -999f;

    int _rollingContactCount = 0;
    bool _hadContactPrevFrame = false;
    float _noContactStartTime = -1f;
    bool _restartArmed = false;
    bool _impactArmed = false;

    void Reset()
    {
        rb = GetComponent<Rigidbody2D>();

        var audios = GetComponents<AudioSource>();
        if (audios.Length > 0) rollAudio = audios[0];
        if (audios.Length > 1) impactAudio = audios[1];

        if (rollTrigger == null)
            rollTrigger = GetComponentInChildren<MarbleRollTrigger2D>();
    }

    void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        SetupAudioSources();

        if (rollTrigger == null)
            rollTrigger = GetComponentInChildren<MarbleRollTrigger2D>();

        _pitchSeed = Random.Range(0.97f, 1.03f);

        if (rollTrigger != null)
            rollTrigger.Init(this);
        else
            Debug.LogWarning($"[{nameof(MarbleRollingAudio)}] rollTrigger가 연결되지 않았습니다: {name}");
    }

    void SetupAudioSources()
    {
        var audios = GetComponents<AudioSource>();

        if (rollAudio == null)
        {
            if (audios.Length > 0) rollAudio = audios[0];
            else rollAudio = gameObject.AddComponent<AudioSource>();
        }

        if (impactAudio == null)
        {
            if (audios.Length > 1) impactAudio = audios[1];
            else impactAudio = gameObject.AddComponent<AudioSource>();
        }

        rollAudio.playOnAwake = false;
        rollAudio.loop = true;
        rollAudio.spatialBlend = 0f;
        rollAudio.panStereo = 0f;
        rollAudio.dopplerLevel = 0f;
        rollAudio.spread = 0f;

        impactAudio.playOnAwake = false;
        impactAudio.loop = false;
        impactAudio.spatialBlend = 0f;
        impactAudio.panStereo = 0f;
        impactAudio.dopplerLevel = 0f;
        impactAudio.spread = 0f;
    }

    void Update()
    {
        bool hasContactNow = _rollingContactCount > 0;

        if (!hasContactNow)
        {
            if (_noContactStartTime < 0f)
                _noContactStartTime = Time.time;

            if (Time.time - _noContactStartTime >= restartAfterNoContactTime)
            {
                _restartArmed = true;
                _impactArmed = true;
            }
        }
        else
        {
            _noContactStartTime = -1f;
        }

        bool contactJustStarted = !_hadContactPrevFrame && hasContactNow;

        float speed = rb.velocity.magnitude;
        bool shouldPlay = hasContactNow && speed >= minRollingSpeed;

        if (contactJustStarted && _restartArmed)
        {
            RestartRollingSound();
            _restartArmed = false;
        }

        if (shouldPlay)
        {
            if (!rollAudio.isPlaying)
            {
                if (rollAudio.clip == null)
                    SelectRandomClip();

                if (rollAudio.clip != null)
                    rollAudio.Play();
            }

            float baseVolume = speed / 6f;
            float finalVolume = Mathf.Clamp(baseVolume * rollingVolumeMultiplier, 0f, rollingMaxVolume);
            rollAudio.volume = finalVolume;
            rollAudio.pitch = _pitchSeed * Mathf.Lerp(0.9f, 1.15f, Mathf.Clamp01(speed / 5f));
        }
        else
        {
            if (rollAudio.isPlaying)
                rollAudio.Stop();
        }

        _hadContactPrevFrame = hasContactNow;
    }

    void RestartRollingSound()
    {
        SelectRandomClip();

        if (rollAudio.isPlaying)
            rollAudio.Stop();

        if (rollAudio.clip != null)
            rollAudio.Play();
    }

    void SelectRandomClip()
    {
        if (rollingClips == null || rollingClips.Length == 0)
        {
            rollAudio.clip = null;
            return;
        }

        if (rollingClips.Length == 1)
        {
            rollAudio.clip = rollingClips[0];
            _lastClipIndex = 0;
            return;
        }

        int index;
        do
        {
            index = Random.Range(0, rollingClips.Length);
        }
        while (index == _lastClipIndex);

        _lastClipIndex = index;
        rollAudio.clip = rollingClips[index];
    }

    public void NotifyRollTriggerEnter(Collider2D other)
    {
        if (other == null) return;
        if (other.isTrigger) return;
        if (!IsInLayerMask(other.gameObject.layer, rollingSurfaceMask))
            return;

        if (_rollingSurfaceColliders.Add(other))
            _rollingContactCount = _rollingSurfaceColliders.Count;
    }

    public void NotifyRollTriggerExit(Collider2D other)
    {
        if (other == null) return;
        if (other.isTrigger) return;
        if (!IsInLayerMask(other.gameObject.layer, rollingSurfaceMask))
            return;

        if (_rollingSurfaceColliders.Remove(other))
            _rollingContactCount = _rollingSurfaceColliders.Count;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {

        if (collision.collider == null) return;
        if (collision.collider.isTrigger) return;
        if (!IsInLayerMask(collision.gameObject.layer, rollingSurfaceMask))
            return;

        if (impactClip == null)
            return;

        if (!_impactArmed)
            return;

        if (Time.time - _lastImpactTime < impactCooldown)
            return;

        _lastImpactTime = Time.time;
        _impactArmed = false;

        if (impactAudio != null)
            impactAudio.PlayOneShot(impactClip, impactVolume);

    }

    bool IsInLayerMask(int layer, LayerMask mask)
    {
        return ((1 << layer) & mask.value) != 0;
    }

    public void PlayDestroySound()
    {
        if (destroyClip == null) return;
        if (impactAudio == null) return;

        impactAudio.PlayOneShot(destroyClip, destroyVolume);
    }

    public void PlayDestroySoundDetached(Vector3 worldPosition)
    {
        if (destroyClip == null) return;

        GameObject temp = new GameObject("DestroySFX");
        temp.transform.position = worldPosition;

        var source = temp.AddComponent<AudioSource>();
        source.clip = destroyClip;
        source.volume = destroyVolume;
        source.loop = false;
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // 2D
        source.panStereo = 0f;
        source.dopplerLevel = 0f;
        source.spread = 0f;

        // impactAudio가 오디오 믹서 쓰고 있으면 그대로 복사
        if (impactAudio != null)
        {
            source.outputAudioMixerGroup = impactAudio.outputAudioMixerGroup;
            source.pitch = impactAudio.pitch;
        }

        source.Play();
        Destroy(temp, destroyClip.length + 0.1f);
    }
}