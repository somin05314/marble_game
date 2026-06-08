using TMPro;
using UnityEngine;

public class PlacementGuideUI : MonoBehaviour
{
    public static PlacementGuideUI I { get; private set; }

    [Header("Refs")]
    [SerializeField] GameObject root;
    [SerializeField] TMP_Text label;
    [SerializeField] RectTransform backgroundPanel;

    [Header("Background Padding")]
    [SerializeField] Vector2 padding = new Vector2(24f, 14f);

    string _current;

    void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        SetGuide(null);
    }

    public void SetGuide(string text)
    {
        _current = text;

        if (label != null)
            label.text = text ?? "";

        RefreshVisible();
    }

    void RefreshVisible()
    {
        bool interactionHintEnabled =
            InteractionHintUI.I == null || InteractionHintUI.I.IsEnabled;

        bool show =
            interactionHintEnabled &&
            !string.IsNullOrEmpty(_current);

        if (root != null)
            root.SetActive(show);

        if (show)
            RefreshBackgroundSize();
    }

    public void RefreshByOption()
    {
        RefreshVisible();
    }

    void RefreshBackgroundSize()
    {
        if (label == null || backgroundPanel == null)
            return;

        label.ForceMeshUpdate();

        Vector2 textSize = label.GetRenderedValues(false);

        backgroundPanel.sizeDelta = new Vector2(
            textSize.x + padding.x,
            textSize.y + padding.y
        );
    }
}