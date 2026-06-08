using UnityEngine;

public class PoMachineAudioPlayer : MonoBehaviour
{
    [SerializeField] AudioSource oneShotSource;
    [SerializeField] AudioSource loopSource;

    [Header("Common")]
    [SerializeField] AudioClip startClip;
    [SerializeField] AudioClip loopClip;
    [SerializeField] AudioClip endClip;

    [Header("Elevator")]
    [SerializeField] AudioClip elevatorLoopClip;

    [Header("Punch Launcher")]
    [SerializeField] AudioClip punchTingClip;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] float volume = 0.5f;
    [SerializeField, Range(0f, 1f)] float liftLoopVolume = 0.25f;
    [SerializeField, Range(0f, 1f)] float elevatorLoopVolume = 0.5f;

    public void PlayStart()
    {
        if (oneShotSource != null && startClip != null)
            oneShotSource.PlayOneShot(startClip, volume);
    }

    public void PlayLoop()
    {
        PlayLoopClip(loopClip, liftLoopVolume);
    }

    public void PlayElevatorLoop()
    {
        PlayLoopClip(elevatorLoopClip, elevatorLoopVolume);
    }

    void PlayLoopClip(AudioClip clip, float clipVolume)
    {
        if (loopSource == null || clip == null) return;

        if (loopSource.isPlaying && loopSource.clip == clip)
            return;

        loopSource.Stop();
        loopSource.clip = clip;
        loopSource.loop = true;
        loopSource.volume = clipVolume;
        loopSource.Play();
    }

    public void StopLoop()
    {
        if (loopSource != null && loopSource.isPlaying)
            loopSource.Stop();
    }

    public void PlayEnd()
    {
        if (oneShotSource != null && endClip != null)
            oneShotSource.PlayOneShot(endClip, volume);
    }

    public void PlayPunchTing()
    {
        if (oneShotSource != null && punchTingClip != null)
            oneShotSource.PlayOneShot(punchTingClip, volume);
    }

    public void StopAll()
    {
        StopLoop();

        if (oneShotSource != null)
            oneShotSource.Stop();
    }
}