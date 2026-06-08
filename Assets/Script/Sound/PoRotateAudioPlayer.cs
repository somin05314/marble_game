using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PoRotateAudioPlayer : MonoBehaviour
{
    [Header("Audio Source")]
    [SerializeField] AudioSource audioSource;

    [Header("Clip")]
    [SerializeField] AudioClip rotateClip;

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

    public void PlayRotateSound()
    {
        if (audioSource == null || rotateClip == null) return;

        audioSource.pitch = useRandomPitch
            ? Random.Range(minPitch, maxPitch)
            : 1f;

        audioSource.PlayOneShot(rotateClip, volume);
    }

    public void PlayRotateStart()
    {
        PlayRotateSound();
    }

    public void StartRotateLoop() { }
    public void StopRotateLoop() { }
    public void PlayRotateEnd() { }

    public void StopAllRotateAudio()
    {
        if (audioSource == null) return;

        audioSource.Stop();
        audioSource.pitch = 1f;
    }
}