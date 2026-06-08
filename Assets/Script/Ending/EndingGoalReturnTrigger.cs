using System.Collections;
using UnityEngine;

public class EndingGoalReturnTrigger : MonoBehaviour
{
    public enum EndingType
    {
        NormalEnding,
        TrueEnding
    }

    [SerializeField] EndingType endingType = EndingType.NormalEnding;

    [SerializeField] LayerMask marbleMask;
    [SerializeField] float delay = 3f;

    [Header("Clear Sound")]
    [SerializeField] AudioSource clearAudioSource;
    [SerializeField] AudioClip clearClip;
    [SerializeField, Range(0f, 1f)] float clearVolume = 1f;

    bool triggered;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;

        if ((marbleMask.value & (1 << other.gameObject.layer)) == 0)
            return;

        triggered = true;

        PlayClearSound();
        UnlockAchievement();

        StartCoroutine(CoReturn());
    }

    void PlayClearSound()
    {
        if (clearAudioSource != null && clearClip != null)
            clearAudioSource.PlayOneShot(clearClip, clearVolume);
    }

    void UnlockAchievement()
    {
        if (AchievementManager.I == null)
            return;

        if (endingType == EndingType.NormalEnding)
            AchievementManager.I.UnlockEndingAchievement();
        else if (endingType == EndingType.TrueEnding)
            AchievementManager.I.UnlockTrueEndingAchievement();
    }

    IEnumerator CoReturn()
    {
        yield return new WaitForSeconds(delay);

        GameModeManager.Instance?.EnterMenuMode();

        if (SceneFlow.I != null)
            SceneFlow.I.GoChapterSelect();
    }
}