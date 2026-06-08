using UnityEngine;

public class SimulationTimeController : MonoBehaviour
{
    [Header("Speeds")]
    [SerializeField] float[] speedSteps = { 0.5f, 1f, 2f, 4f, 8f };
    [SerializeField] int defaultIndex = 1; // 1x

    [Header("Options")]
    [SerializeField] bool simulationOnly = true;

#if UNITY_EDITOR
    [Header("DEV Hotkeys (Editor)")]
    [SerializeField] bool enableHotkeys = true;
    [SerializeField] KeyCode slowerKey = KeyCode.LeftBracket;   // [
    [SerializeField] KeyCode fasterKey = KeyCode.RightBracket;  // ]
    [SerializeField] KeyCode resetKey = KeyCode.BackQuote;     // `
#endif

    int currentIndex;

    void Awake()
    {
        currentIndex = Mathf.Clamp(defaultIndex, 0, speedSteps.Length - 1);

        // 빌드 씬 들어왔을 때 1x 강제 (안전)
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }


    void Update()
    {
#if UNITY_EDITOR
        if (!enableHotkeys) return;

        if (!CanControlNow()) return;

        if (Input.GetKeyDown(slowerKey)) Step(-1);
        if (Input.GetKeyDown(fasterKey)) Step(+1);
        if (Input.GetKeyDown(resetKey)) SetIndex(defaultIndex);
#endif
    }

    bool CanControlNow()
    {
        if (!simulationOnly) return true;

        // ✅ 네 프로젝트에서 "실행 모드" 판정 로직에 맞춰 교체
        // 예: GameModeManager.Instance.currentMode == GameMode.Simulate
        return GameModeManager.Instance != null
            && GameModeManager.Instance.currentMode == GameMode.Play;
    }

    public float CurrentSpeed => speedSteps[currentIndex];

    public void Step(int delta)
    {
        SetIndex(currentIndex + delta);
    }

    public void SetIndex(int index)
    {
        currentIndex = Mathf.Clamp(index, 0, speedSteps.Length - 1);
        Apply();
    }

    public void SetSpeed(float speed)
    {
        // speedSteps에 없으면 가장 가까운 값으로
        int best = 0;
        float bestDist = float.MaxValue;

        for (int i = 0; i < speedSteps.Length; i++)
        {
            float d = Mathf.Abs(speedSteps[i] - speed);
            if (d < bestDist) { bestDist = d; best = i; }
        }

        SetIndex(best);
    }

    public void ResetToNormal()
    {
        SetIndex(defaultIndex);
    }

    void Apply()
    {
        Time.timeScale = speedSteps[currentIndex];
        Time.fixedDeltaTime = Mathf.Min(0.02f * Time.timeScale, 0.05f);
#if UNITY_EDITOR
        Debug.Log($"[TimeScale] x{Time.timeScale} (fixedDelta={Time.fixedDeltaTime})");
#endif
    }

    void OnDisable()
    {
        // 씬 꺼질 때 안전하게 복구
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }
}
