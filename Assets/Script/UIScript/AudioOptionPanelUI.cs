using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class AudioOptionPanelUI : MonoBehaviour
{
    [Header("Mixer")]
    [SerializeField] AudioMixer mixer;

    [Header("Exposed Parameter Names")]
    [SerializeField] string masterParam = "MasterVolume";
    [SerializeField] string bgmParam = "BGMVolume";
    [SerializeField] string sfxParam = "SFXVolume";
    [SerializeField] string uiParam = "UIVolume";

    [Header("Sliders")]
    [SerializeField] Slider masterSlider;
    [SerializeField] Slider bgmSlider;
    [SerializeField] Slider sfxSlider;
    [SerializeField] Slider uiSlider;

    const string MasterKey = "Audio_Master";
    const string BgmKey = "Audio_BGM";
    const string SfxKey = "Audio_SFX";
    const string UiKey = "Audio_UI";

    bool _ignoreCallback;

    void Start()
    {
        float master = PlayerPrefs.GetFloat(MasterKey, 1f);
        float bgm = PlayerPrefs.GetFloat(BgmKey, 1f);
        float sfx = PlayerPrefs.GetFloat(SfxKey, 1f);
        float ui = PlayerPrefs.GetFloat(UiKey, 1f);

        _ignoreCallback = true;

        if (masterSlider != null) masterSlider.value = master;
        if (bgmSlider != null) bgmSlider.value = bgm;
        if (sfxSlider != null) sfxSlider.value = sfx;
        if (uiSlider != null) uiSlider.value = ui;

        _ignoreCallback = false;

        ApplyVolume(masterParam, master);
        ApplyVolume(bgmParam, bgm);
        ApplyVolume(sfxParam, sfx);
        ApplyVolume(uiParam, ui);

        if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        if (uiSlider != null) uiSlider.onValueChanged.AddListener(OnUiChanged);
    }

    void OnDestroy()
    {
        if (masterSlider != null) masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
        if (bgmSlider != null) bgmSlider.onValueChanged.RemoveListener(OnBgmChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
        if (uiSlider != null) uiSlider.onValueChanged.RemoveListener(OnUiChanged);
    }

    void OnMasterChanged(float value)
    {
        if (_ignoreCallback) return;

        ApplyVolume(masterParam, value);
        PlayerPrefs.SetFloat(MasterKey, value);
        PlayerPrefs.Save();
    }

    void OnBgmChanged(float value)
    {
        if (_ignoreCallback) return;

        ApplyVolume(bgmParam, value);
        PlayerPrefs.SetFloat(BgmKey, value);
        PlayerPrefs.Save();
    }

    void OnSfxChanged(float value)
    {
        if (_ignoreCallback) return;

        ApplyVolume(sfxParam, value);
        PlayerPrefs.SetFloat(SfxKey, value);
        PlayerPrefs.Save();
    }

    void OnUiChanged(float value)
    {
        if (_ignoreCallback) return;

        ApplyVolume(uiParam, value);
        PlayerPrefs.SetFloat(UiKey, value);
        PlayerPrefs.Save();
    }

    void ApplyVolume(string parameterName, float normalizedValue)
    {
        if (mixer == null || string.IsNullOrEmpty(parameterName))
            return;

        float db = NormalizedToDb(normalizedValue);
        mixer.SetFloat(parameterName, db);
    }

    float NormalizedToDb(float value)
    {
        if (value <= 0.0001f)
            return -80f;

        return Mathf.Log10(value) * 20f;
    }
}