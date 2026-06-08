using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PoMoveAudioPlayer : MonoBehaviour
{
    [Header("Audio Source")]
    [SerializeField] AudioSource audioSource;

    [Header("Clip")]
    [SerializeField] AudioClip moveClip;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] float volume = 0.5f;

    [Header("Random Pitch")]
    [SerializeField] bool useRandomPitch = true;
    [SerializeField, Range(0.8f, 1.2f)] float minPitch = 0.96f;
    [SerializeField, Range(0.8f, 1.2f)] float maxPitch = 1.04f;

    void Reset()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }
    }

    void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }
    }

    public void PlayMoveSound()
    {
        if (audioSource == null || moveClip == null) return;

        audioSource.pitch = useRandomPitch
            ? Random.Range(minPitch, maxPitch)
            : 1f;

        audioSource.PlayOneShot(moveClip, volume);
    }

    public void PlayMoveStart()
    {
        PlayMoveSound();
    }

    public void StartMoveLoop() { }
    public void StopMoveLoop() { }
    public void PlayMoveEnd() { }

    public void StopAllMoveAudio()
    {
        if (audioSource == null) return;

        audioSource.Stop();
        audioSource.pitch = 1f;
    }
}