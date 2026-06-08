using UnityEngine;

public enum UIButtonSoundType
{
    Enter,
    Apply,
    Release,
    Hover
}

[RequireComponent(typeof(AudioSource))]
public class UISoundManager : MonoBehaviour
{
    public static UISoundManager I { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] AudioSource uiAudioSource;
    [SerializeField] AudioSource worldAudioSource;

    [Header("UI Clips")]
    [SerializeField] AudioClip enterClip;
    [SerializeField] AudioClip applyClip;
    [SerializeField] AudioClip releaseClip;
    [SerializeField] AudioClip hoverClip;

    [Header("UI Volume")]
    [SerializeField, Range(0f, 1f)] float enterVolume = 1f;
    [SerializeField, Range(0f, 1f)] float applyVolume = 1f;
    [SerializeField, Range(0f, 1f)] float releaseVolume = 1f;
    [SerializeField, Range(0f, 1f)] float hoverVolume = 0.7f;

    [Header("Rail Clips")]
    [SerializeField] AudioClip railPlaceClip;
    [SerializeField] AudioClip railSelectClip;
    [SerializeField] AudioClip railDeselectClip;
    [SerializeField] AudioClip railDeleteClip;
    [SerializeField] AudioClip railSnapConnectClip;

    [Header("Rail Volume")]
    [SerializeField, Range(0f, 1f)] float railPlaceVolume = 1f;
    [SerializeField, Range(0f, 1f)] float railSelectVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] float railDeselectVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] float railDeleteVolume = 1f;
    [SerializeField, Range(0f, 1f)] float railSnapConnectVolume = 0.9f;

    [Header("PO Clips")]
    [SerializeField] AudioClip poPlaceClip;
    [SerializeField] AudioClip poSelectClip;
    [SerializeField] AudioClip poDeselectClip;
    [SerializeField] AudioClip poDeleteClip;

    [Header("PO Volume")]
    [SerializeField, Range(0f, 1f)] float poPlaceVolume = 1f;
    [SerializeField, Range(0f, 1f)] float poSelectVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] float poDeselectVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] float poDeleteVolume = 1f;

    void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;

        // uiAudioSource가 비어 있으면 현재 오브젝트의 AudioSource를 UI용으로 사용
        if (uiAudioSource == null)
            uiAudioSource = GetComponent<AudioSource>();

        // worldAudioSource가 비어 있으면 자동 생성
        if (worldAudioSource == null)
        {
            GameObject go = new GameObject("WorldAudioSource");
            go.transform.SetParent(transform, false);
            worldAudioSource = go.AddComponent<AudioSource>();

            // 필요하면 여기서 기본 설정 복사
            worldAudioSource.playOnAwake = false;
            worldAudioSource.loop = false;
            worldAudioSource.spatialBlend = 0f; // 2D 사운드
        }

        if (uiAudioSource != null)
        {
            uiAudioSource.playOnAwake = false;
            uiAudioSource.loop = false;
            uiAudioSource.spatialBlend = 0f; // UI도 2D
        }
    }

    #region UI
    public void PlayEnter()
    {
        PlayUIOneShot(enterClip, enterVolume);
    }

    public void PlayApply()
    {
        PlayUIOneShot(applyClip, applyVolume);
    }

    public void PlayRelease()
    {
        PlayUIOneShot(releaseClip, releaseVolume);
    }

    public void PlayHover()
    {
        PlayUIOneShot(hoverClip, hoverVolume);
    }

    public void Play(UIButtonSoundType type)
    {
        switch (type)
        {
            case UIButtonSoundType.Enter:
                PlayEnter();
                break;

            case UIButtonSoundType.Apply:
                PlayApply();
                break;

            case UIButtonSoundType.Release:
                PlayRelease();
                break;

            case UIButtonSoundType.Hover:
                PlayHover();
                break;
        }
    }
    #endregion

    #region Rail
    public void PlayRailPlace()
    {
        PlayWorldOneShot(railPlaceClip, railPlaceVolume);
    }

    public void PlayRailSelect()
    {
        PlayWorldOneShot(railSelectClip, railSelectVolume);
    }

    public void PlayRailDeselect()
    {
        PlayWorldOneShot(railDeselectClip, railDeselectVolume);
    }

    public void PlayRailDelete()
    {
        PlayWorldOneShot(railDeleteClip, railDeleteVolume);
    }
    #endregion

    #region PO
    public void PlayPOPlace()
    {
        PlayWorldOneShot(poPlaceClip, poPlaceVolume);
    }

    public void PlayPOSelect()
    {
        PlayWorldOneShot(poSelectClip, poSelectVolume);
    }

    public void PlayPODeselect()
    {
        PlayWorldOneShot(poDeselectClip, poDeselectVolume);
    }

    public void PlayPODelete()
    {
        PlayWorldOneShot(poDeleteClip, poDeleteVolume);
    }
    #endregion

    void PlayUIOneShot(AudioClip clip, float volume)
    {
        if (clip == null || uiAudioSource == null)
            return;

        uiAudioSource.PlayOneShot(clip, volume);
    }

    void PlayWorldOneShot(AudioClip clip, float volume)
    {
        if (clip == null || worldAudioSource == null)
            return;

        worldAudioSource.PlayOneShot(clip, volume);
    }

    public void PlayRailSnapConnect()
    {
        PlayWorldOneShot(railSnapConnectClip, railSnapConnectVolume);
    }
}