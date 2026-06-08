using System.Collections.Generic;
using UnityEngine;

public class StageHintController : MonoBehaviour
{
    [Header("Hint Roots (순서대로 넣기)")]
    [Tooltip("Step1, Step2, Step3, Path 순서처럼 힌트 단계를 순서대로 넣어주세요.")]
    [SerializeField] GameObject hintRoot;

    [SerializeField] List<GameObject> stepRoots = new List<GameObject>();

    [Header("State")]
    [Tooltip("-1 = 힌트 없음, 0 = 첫 번째 단계 표시, 1 = 두 번째 단계까지 표시")]
    [SerializeField] int currentStepIndex = -1;

    [SerializeField] bool isVisible = false;

    public int CurrentStepIndex => currentStepIndex;
    public bool IsVisible => isVisible;
    public int StepCount => stepRoots != null ? stepRoots.Count : 0;
    public bool HasAnyStep => StepCount > 0;

    void Awake()
    {
        RefreshView();
    }

    /// <summary>
    /// 힌트 버튼:
    /// -1 -> 0 -> 1 -> 2 -> ... -> 마지막 -> -1
    /// 누르면 항상 보이기 상태가 됨
    /// </summary>
    public void NextHintStep()
    {
        if (!HasAnyStep)
        {
            currentStepIndex = -1;
            isVisible = false;
            RefreshView();
            return;
        }

        currentStepIndex++;

        if (currentStepIndex >= StepCount)
        {
            currentStepIndex = -1;
            isVisible = false;
        }
        else
        {
            isVisible = true;
        }

        RefreshView();
    }

    /// <summary>
    /// 현재 단계 유지, 표시만 토글
    /// </summary>
    public void ToggleHintVisible()
    {
        if (currentStepIndex < 0 || !HasAnyStep)
            return;

        isVisible = !isVisible;
        RefreshView();
    }

    /// <summary>
    /// 현재 단계는 유지하고, 힌트 표시만 강제로 끔
    /// 플레이 모드 진입 시 사용
    /// </summary>
    public void HideHints()
    {
        if (!HasAnyStep)
        {
            currentStepIndex = -1;
            isVisible = false;
            RefreshView();
            return;
        }

        isVisible = false;
        RefreshView();
    }

    /// <summary>
    /// 현재 단계가 있을 때만 다시 표시
    /// </summary>
    public void ShowHints()
    {
        if (currentStepIndex < 0 || !HasAnyStep)
            return;

        isVisible = true;
        RefreshView();
    }

    /// <summary>
    /// 단계 자체를 초기화
    /// </summary>
    public void ResetHints()
    {
        currentStepIndex = -1;
        isVisible = false;
        RefreshView();
    }

    /// <summary>
    /// 특정 단계로 바로 설정
    /// 0 = 첫 번째 단계, 1 = 두 번째 단계 ...
    /// -1 = 힌트 없음
    /// </summary>
    public void SetHintStep(int stepIndex, bool forceVisible = true)
    {
        if (!HasAnyStep)
        {
            currentStepIndex = -1;
            isVisible = false;
            RefreshView();
            return;
        }

        if (stepIndex < 0)
        {
            currentStepIndex = -1;
            isVisible = false;
        }
        else
        {
            currentStepIndex = Mathf.Clamp(stepIndex, 0, StepCount - 1);
            isVisible = forceVisible;
        }

        RefreshView();
    }

    public void RefreshView()
    {
        bool showAny = isVisible && currentStepIndex >= 0 && HasAnyStep;

        if (hintRoot != null)
            hintRoot.SetActive(showAny);

        if (stepRoots == null)
            return;

        for (int i = 0; i < stepRoots.Count; i++)
        {
            var root = stepRoots[i];
            if (root == null)
                continue;

            // currentStepIndex가 1이면 0,1 단계까지 켜짐 (누적 표시)
            root.SetActive(showAny && i <= currentStepIndex);
        }
    }
}