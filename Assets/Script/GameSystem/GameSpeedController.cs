using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameSpeedController : MonoBehaviour
{
    const string SpeedIndexKey = "GameSpeedIndex";

    [Header("UI")]
    [SerializeField] Slider speedSlider;
    [SerializeField] TMP_Text speedText;

    [Header("Speed Steps")]
    [SerializeField] float[] speeds = { 1f, 1.5f, 2f, 2.5f, 3f };

    float baseFixedDeltaTime;
    float currentSelectedSpeed = 1f;
    bool isPlayMode;

    void Awake()
    {
        baseFixedDeltaTime = Time.fixedDeltaTime;

        int savedIndex = PlayerPrefs.GetInt(SpeedIndexKey, 0);
        savedIndex = Mathf.Clamp(savedIndex, 0, speeds.Length - 1);

        currentSelectedSpeed = speeds[savedIndex];

        if (speedSlider != null)
        {
            speedSlider.minValue = 0;
            speedSlider.maxValue = speeds.Length - 1;
            speedSlider.wholeNumbers = true;

            // 리스너 등록 전에 값 세팅
            speedSlider.value = savedIndex;

            speedSlider.onValueChanged.AddListener(OnSliderChanged);
        }

        ApplySpeed(1f);
        RefreshText();
    }

    void OnEnable()
    {
        GameModeManager.OnModeChanged += OnGameModeChanged;

        if (GameModeManager.Instance != null)
            OnGameModeChanged(GameModeManager.Instance.currentMode);
    }

    void OnDisable()
    {
        GameModeManager.OnModeChanged -= OnGameModeChanged;
    }

    void OnDestroy()
    {
        if (speedSlider != null)
            speedSlider.onValueChanged.RemoveListener(OnSliderChanged);

        ResetSpeed();
    }

    void OnSliderChanged(float value)
    {
        int index = Mathf.RoundToInt(value);
        index = Mathf.Clamp(index, 0, speeds.Length - 1);

        currentSelectedSpeed = speeds[index];

        PlayerPrefs.SetInt(SpeedIndexKey, index);
        PlayerPrefs.Save();

        if (isPlayMode)
            ApplySpeed(currentSelectedSpeed);
        else
            ApplySpeed(1f);

        RefreshText();
    }

    void OnGameModeChanged(GameMode mode)
    {
        isPlayMode = mode == GameMode.Play;

        if (isPlayMode)
            ApplySpeed(currentSelectedSpeed);
        else
            ApplySpeed(1f);

        RefreshText();
    }

    void ApplySpeed(float speed)
    {
        Time.timeScale = speed;
        Time.fixedDeltaTime = baseFixedDeltaTime;

        Debug.Log($"[GameSpeed] Speed set to x{speed}");
    }

    void ResetSpeed()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDeltaTime;
    }

    void RefreshText()
    {
        if (speedText != null)
            speedText.text = $"x{currentSelectedSpeed:0.#}";
    }
}