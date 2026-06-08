using UnityEngine;
using UnityEngine.UI;

public class UIButtonSound : MonoBehaviour
{
    [Header("Sound Type")]
    [SerializeField] UIButtonSoundType clickSoundType = UIButtonSoundType.Apply;

    Button _button;
    Toggle _toggle;

    void Awake()
    {
        _button = GetComponent<Button>();
        _toggle = GetComponent<Toggle>();

        if (_button != null)
            _button.onClick.AddListener(HandleClick);

        if (_toggle != null)
            _toggle.onValueChanged.AddListener(HandleToggleChanged);
    }

    void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(HandleClick);

        if (_toggle != null)
            _toggle.onValueChanged.RemoveListener(HandleToggleChanged);
    }

    public void Play()
    {
        if (UISoundManager.I == null)
            return;

        UISoundManager.I.Play(clickSoundType);
    }

    void HandleClick()
    {
        Play();
    }

    void HandleToggleChanged(bool isOn)
    {
        Play();
    }
}