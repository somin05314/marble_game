using UnityEngine;
using UnityEngine.UI;

public class GoalHitFeedbackOptionUI : MonoBehaviour
{
    [Header("Optional UI")]
    [SerializeField] Toggle toggle;

    bool _ignoreCallback;

    void Start()
    {
        if (toggle == null)
            toggle = GetComponent<Toggle>();

        bool isOn = GoalHitFeedbackOption.Enabled;

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

        GoalHitFeedbackOption.Enabled = isOn;
    }
}