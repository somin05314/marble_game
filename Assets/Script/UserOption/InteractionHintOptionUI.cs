using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractionHintOptionUI : MonoBehaviour
{
    [Header("Optional UI")]
    [SerializeField] Toggle toggle;

    bool _ignoreCallback;

    void Start()
    {
        bool isOn = true;

        if (InteractionHintUI.I != null)
            isOn = InteractionHintUI.I.IsEnabled;

        _ignoreCallback = true;

        if (toggle != null)
            toggle.isOn = isOn;

        _ignoreCallback = false;

        if (toggle != null)
            toggle.onValueChanged.AddListener(OnToggleChanged);
    }

    void OnDestroy()
    {
        if (toggle != null)
            toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }

    void OnToggleChanged(bool isOn)
    {
        if (_ignoreCallback)
            return;

        if (InteractionHintUI.I != null)
            InteractionHintUI.I.SetEnabledOption(isOn);

    }

}