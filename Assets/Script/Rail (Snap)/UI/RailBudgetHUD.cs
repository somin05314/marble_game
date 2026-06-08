using UnityEngine;
using TMPro;

public class RailBudgetHUD : MonoBehaviour
{
    [SerializeField] TMP_Text text;
    [SerializeField] bool hideWhenUnlimited = false;

    void Reset()
    {
        text = GetComponent<TMP_Text>();
    }

    void Update()
    {
        var b = RailBudget2D.Instance;
        if (text == null) return;

        if (b == null)
        {
            text.text = "";
            return;
        }

        if (!b.IsLimited)
        {
            if (hideWhenUnlimited) { text.text = ""; return; }
            text.text = $"{b.UsedRails} / ¡Ä";
        }
        else
        {
            text.text = $"{b.UsedRails} / {b.MaxRails}";
        }
    }
}
