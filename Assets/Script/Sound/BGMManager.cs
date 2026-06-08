using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BGMManager : MonoBehaviour
{
    public static BGMManager I { get; private set; }

    [Header("Audio Source")]
    [SerializeField] AudioSource audioSource;

    [Header("Playlist")]
    [SerializeField] List<AudioClip> playlist = new List<AudioClip>();
    [SerializeField, Range(0f, 1f)] float defaultVolume = 0.6f;

    [Header("Random")]
    [Tooltip("가능하면 바로 직전 곡은 다시 뽑지 않음")]
    [SerializeField] bool avoidImmediateRepeat = true;

    [Header("Fade")]
    [SerializeField] float fadeDuration = 0.4f;

    Coroutine fadeCo;
    Coroutine monitorCo;

    AudioClip currentClip;
    int currentIndex = -1;
    bool stopping;

    void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        audioSource.loop = false; // 랜덤 다음 곡으로 넘어가야 하므로 false
        audioSource.playOnAwake = false;
    }

    void Start()
    {
        PlayRandom(immediate: true);
    }

    // -----------------------------
    // Public
    // -----------------------------

    public void PlayRandom(bool immediate = false)
    {
        int nextIndex = GetRandomNextIndex();
        if (nextIndex < 0) return;

        PlayByIndex(nextIndex, immediate);
    }

    public void PlayByIndex(int index, bool immediate = false)
    {
        if (playlist == null || playlist.Count == 0) return;
        if (index < 0 || index >= playlist.Count) return;

        var clip = playlist[index];
        if (clip == null) return;

        currentIndex = index;
        currentClip = clip;
        stopping = false;

        if (fadeCo != null)
            StopCoroutine(fadeCo);

        if (immediate || !audioSource.isPlaying)
        {
            audioSource.clip = clip;
            audioSource.volume = defaultVolume;
            audioSource.Play();
            RestartMonitor();
            return;
        }

        fadeCo = StartCoroutine(Co_ChangeClip(clip, defaultVolume));
    }

    public void Play(AudioClip clip, float volume = 1f, bool immediate = false)
    {
        if (clip == null) return;

        stopping = false;

        if (fadeCo != null)
            StopCoroutine(fadeCo);

        currentClip = clip;
        currentIndex = FindClipIndex(clip);

        if (currentClip == clip && audioSource.isPlaying)
        {
            audioSource.volume = volume;
            return;
        }

        if (immediate || !audioSource.isPlaying)
        {
            audioSource.clip = clip;
            audioSource.volume = volume;
            audioSource.Play();
            RestartMonitor();
            return;
        }

        fadeCo = StartCoroutine(Co_ChangeClip(clip, volume));
    }

    public void StopBGM(bool immediate = false)
    {
        stopping = true;

        if (fadeCo != null)
            StopCoroutine(fadeCo);

        if (monitorCo != null)
        {
            StopCoroutine(monitorCo);
            monitorCo = null;
        }

        if (immediate)
        {
            audioSource.Stop();
            currentClip = null;
            currentIndex = -1;
            return;
        }

        fadeCo = StartCoroutine(Co_Stop());
    }

    public void SetPlaylist(List<AudioClip> newPlaylist, bool playNow = true, bool immediate = false)
    {
        playlist = newPlaylist ?? new List<AudioClip>();

        if (playNow)
            PlayRandom(immediate);
    }

    public void AddToPlaylist(AudioClip clip)
    {
        if (clip == null) return;

        if (playlist == null)
            playlist = new List<AudioClip>();

        playlist.Add(clip);
    }

    // -----------------------------
    // Internals
    // -----------------------------

    void RestartMonitor()
    {
        if (monitorCo != null)
            StopCoroutine(monitorCo);

        monitorCo = StartCoroutine(Co_MonitorTrackEnd());
    }

    IEnumerator Co_MonitorTrackEnd()
    {
        while (true)
        {
            if (stopping)
            {
                monitorCo = null;
                yield break;
            }

            if (!Application.isFocused)
            {
                yield return null;
                continue;
            }

            if (!audioSource.isPlaying && audioSource.clip != null)
            {
                PlayRandom(immediate: true);
                monitorCo = null;
                yield break;
            }

            yield return null;
        }
    }

    IEnumerator Co_ChangeClip(AudioClip nextClip, float targetVolume)
    {
        float startVolume = audioSource.volume;

        if (audioSource.isPlaying && fadeDuration > 0f)
        {
            for (float t = 0; t < fadeDuration; t += Time.unscaledDeltaTime)
            {
                audioSource.volume = Mathf.Lerp(startVolume, 0f, t / fadeDuration);
                yield return null;
            }
        }

        audioSource.volume = 0f;
        audioSource.clip = nextClip;
        audioSource.Play();

        RestartMonitor();

        if (fadeDuration > 0f)
        {
            for (float t = 0; t < fadeDuration; t += Time.unscaledDeltaTime)
            {
                audioSource.volume = Mathf.Lerp(0f, targetVolume, t / fadeDuration);
                yield return null;
            }
        }

        audioSource.volume = targetVolume;
        fadeCo = null;
    }

    IEnumerator Co_Stop()
    {
        float startVolume = audioSource.volume;

        if (fadeDuration > 0f)
        {
            for (float t = 0; t < fadeDuration; t += Time.unscaledDeltaTime)
            {
                audioSource.volume = Mathf.Lerp(startVolume, 0f, t / fadeDuration);
                yield return null;
            }
        }

        audioSource.volume = 0f;
        audioSource.Stop();
        currentClip = null;
        currentIndex = -1;
        fadeCo = null;
    }

    int GetRandomNextIndex()
    {
        if (playlist == null || playlist.Count == 0)
            return -1;

        List<int> valid = new List<int>();

        for (int i = 0; i < playlist.Count; i++)
        {
            if (playlist[i] == null) continue;

            if (avoidImmediateRepeat && playlist.Count > 1 && i == currentIndex)
                continue;

            valid.Add(i);
        }

        if (valid.Count == 0)
        {
            // 전부 null이거나, 유일한 곡만 남은 경우 currentIndex 허용
            for (int i = 0; i < playlist.Count; i++)
            {
                if (playlist[i] != null)
                    return i;
            }

            return -1;
        }

        int pick = Random.Range(0, valid.Count);
        return valid[pick];
    }

    int FindClipIndex(AudioClip clip)
    {
        if (playlist == null || clip == null) return -1;

        for (int i = 0; i < playlist.Count; i++)
        {
            if (playlist[i] == clip)
                return i;
        }

        return -1;
    }
}