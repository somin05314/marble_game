using UnityEngine;

public class SciFiAudioPlayer : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] AudioSource oneShotSource;
    [SerializeField] AudioSource loopSource;

    [Header("One Shot Clips")]
    [SerializeField] AudioClip powerOnClip;
    [SerializeField] AudioClip teleportClip;
    [SerializeField] AudioClip warpClip;
    [SerializeField] AudioClip laserFireClip;
    [SerializeField] AudioClip doorOpenClip;

    [Header("Loop Clips")]
    [SerializeField] AudioClip laserLoopClip;
    [SerializeField] AudioClip amplifierLoopClip;
    [SerializeField] AudioClip blackholeLoopClip;

    void Reset()
    {
        oneShotSource = GetComponent<AudioSource>();
    }

    void Awake()
    {
        if (oneShotSource != null)
        {
            oneShotSource.playOnAwake = false;
            oneShotSource.loop = false;
        }

        if (loopSource != null)
        {
            loopSource.playOnAwake = false;
            loopSource.loop = true;
        }
    }

    public void PlayPowerOn() => PlayOneShot(powerOnClip);
    public void PlayTeleport() => PlayOneShot(teleportClip);
    public void PlayWarp() => PlayOneShot(warpClip);
    public void PlayLaserFire() => PlayOneShot(laserFireClip);
    public void PlayDoorOpen() => PlayOneShot(doorOpenClip);

    public void StartLaserLoop() => StartLoop(laserLoopClip);
    public void StartAmplifierLoop() => StartLoop(amplifierLoopClip);

    public void StopLoop()
    {
        if (loopSource == null) return;

        loopSource.Stop();
        loopSource.clip = null;
    }

    public void StopAll()
    {
        if (oneShotSource != null)
            oneShotSource.Stop();

        StopLoop();
    }

    void PlayOneShot(AudioClip clip)
    {
        if (clip == null || oneShotSource == null) return;

        oneShotSource.PlayOneShot(clip);
    }

    void StartLoop(AudioClip clip)
    {
        if (clip == null || loopSource == null) return;

        if (loopSource.clip == clip && loopSource.isPlaying)
            return;

        loopSource.clip = clip;
        loopSource.loop = true;
        loopSource.Play();
    }




    public void StartBlackholeLoop() => StartLoop(blackholeLoopClip);
}