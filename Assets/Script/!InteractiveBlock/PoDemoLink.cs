using System.Collections;
using UnityEngine;

public interface IPoDirectDemoPlayable
{
    void BeginDirectDemo();
    void EndDirectDemo();
}

public class PoDemoLink : MonoBehaviour
{
    [Header("Demo Trigger (Fallback)")]
    [Tooltip("기존 단일 버튼 방식. 강도별 버튼이 없으면 이 값을 사용합니다.")]
    [SerializeField] TriggerZone demoTrigger;

    [Header("Demo Trigger By Level (Optional)")]
    [Tooltip("비워두면 fallback demoTrigger를 사용합니다.")]
    [SerializeField] TriggerZone level1DemoTrigger;
    [SerializeField] TriggerZone level2DemoTrigger;
    [SerializeField] TriggerZone level3DemoTrigger;

    [Header("Direct Demo (Optional)")]
    [Tooltip("트리거 버튼 없이 직접 실행되는 데모 대상. 예: LiftMarbleObjectController")]
    [SerializeField] MonoBehaviour directDemoTarget;

    IPoDirectDemoPlayable _directDemoPlayable;

    [Header("Reset Scope")]
    [Tooltip("비워두면 자기 자신(transform) 기준으로 리셋 대상을 찾습니다.")]
    [SerializeField] Transform resetRoot;

    [Tooltip("자식까지 포함해서 IPoResettable을 모두 찾아 ResetState()를 호출합니다.")]
    [SerializeField] bool includeChildren = true;

    [Header("Demo Timing")]
    [Tooltip("버튼을 눌러서 동작이 진행되는 시간 / 직접 실행형 데모 실행 시간")]
    [SerializeField] float demoDuration = 1.2f;

    [Tooltip("버튼을 뗀 뒤 결과를 잠깐 보여주는 시간 / 직접 실행 종료 후 대기 시간")]
    [SerializeField] float resetDelayAfterDemo = 0.5f;

    [Header("Strength Timing")]
    [SerializeField] StrengthBasedOccupancyCells strengthComp;
    [SerializeField] bool useStrengthBasedTiming = false;
    [SerializeField] LevelDemoTiming[] levelTimings;

    [System.Serializable]
    public struct LevelDemoTiming
    {
        public int level;
        public float demoDuration;
        public float resetDelayAfterDemo;
    }

    public bool IsDemoPlaying => _demoCo != null;

    Coroutine _demoCo;

    void Awake()
    {
        if (resetRoot == null)
            resetRoot = transform;

        if (strengthComp == null)
            strengthComp = GetComponent<StrengthBasedOccupancyCells>();

        CacheDirectDemoTarget();
    }

    void OnValidate()
    {
        CacheDirectDemoTarget();
    }

    void CacheDirectDemoTarget()
    {
        _directDemoPlayable = directDemoTarget as IPoDirectDemoPlayable;

        Debug.Log($"[PoDemoLink] directDemoTarget: {directDemoTarget}, cast success: {_directDemoPlayable != null}");
    }

    public void PlayDemo()
    {
        StopDemoAndReset();
        _demoCo = StartCoroutine(CoPlayDemo());
    }

    public void StopDemoIfPlaying()
    {
        if (_demoCo != null)
            StopDemoAndReset();
    }

    public void StopDemoAndReset()
    {
        if (_demoCo != null)
        {
            StopCoroutine(_demoCo);
            _demoCo = null;
        }

        // 직접 실행형 데모 정지
        if (_directDemoPlayable != null)
        {
            _directDemoPlayable.EndDirectDemo();
        }

        // 기존 트리거형 데모 정지
        TriggerZone trigger = GetCurrentDemoTrigger();
        if (trigger != null)
        {
            trigger.StopDemo(false);
            trigger.ForceExit();
        }

        ResetAllTargets();
    }

    IEnumerator CoPlayDemo()
    {
        ResetAllTargets();
        yield return null;

        // 1. 직접 실행형 데모 우선
        if (_directDemoPlayable != null)
        {
            yield return PlayDirectDemoCycle();
            ResetAllTargets();
            _demoCo = null;
            yield break;
        }

        // 2. 없으면 기존 트리거 방식 fallback
        TriggerZone trigger = GetCurrentDemoTrigger();
        if (trigger == null)
        {
            _demoCo = null;
            yield break;
        }

        // 1차: OFF -> ON
        yield return PlaySinglePressCycle(trigger);

        // 토글 버튼이면 2차: ON -> OFF
        if (trigger.UseToggleMode)
            yield return PlaySinglePressCycle(trigger);

        ResetAllTargets();
        _demoCo = null;
    }

    IEnumerator PlayDirectDemoCycle()
    {
        if (_directDemoPlayable == null)
            yield break;

        GetCurrentDemoTiming(out float duration, out float resetDelay);

        _directDemoPlayable.BeginDirectDemo();

        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        _directDemoPlayable.EndDirectDemo();

        if (resetDelay > 0f)
            yield return new WaitForSeconds(resetDelay);
    }

    IEnumerator PlaySinglePressCycle(TriggerZone trigger)
    {
        if (trigger == null)
            yield break;

        GetCurrentDemoTiming(out float duration, out float resetDelay);

        trigger.ForceEnter();

        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        trigger.ForceExit();

        if (resetDelay > 0f)
            yield return new WaitForSeconds(resetDelay);
    }

    TriggerZone GetCurrentDemoTrigger()
    {
        int level = GetCurrentStrengthLevel();

        TriggerZone levelTrigger = null;

        switch (level)
        {
            case 1:
                levelTrigger = level1DemoTrigger;
                break;
            case 2:
                levelTrigger = level2DemoTrigger;
                break;
            case 3:
                levelTrigger = level3DemoTrigger;
                break;
        }

        if (levelTrigger != null)
            return levelTrigger;

        return demoTrigger;
    }

    int GetCurrentStrengthLevel()
    {
        if (strengthComp != null)
            return strengthComp.CurrentLevel;

        return 1;
    }

    public void ResetAllTargets()
    {
        if (resetRoot == null)
            resetRoot = transform;

        if (!includeChildren)
        {
            var list = resetRoot.GetComponents<MonoBehaviour>();
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] is IPoResettable resettable)
                    resettable.ResetState();
            }
            return;
        }

        var all = resetRoot.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is IPoResettable resettable)
                resettable.ResetState();
        }
    }

    void GetCurrentDemoTiming(out float duration, out float resetDelay)
    {
        duration = demoDuration;
        resetDelay = resetDelayAfterDemo;

        if (!useStrengthBasedTiming || strengthComp == null || levelTimings == null)
            return;

        int level = strengthComp.CurrentLevel;

        for (int i = 0; i < levelTimings.Length; i++)
        {
            if (levelTimings[i].level == level)
            {
                duration = levelTimings[i].demoDuration;
                resetDelay = levelTimings[i].resetDelayAfterDemo;
                return;
            }
        }
    }
}