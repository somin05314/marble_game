using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance;

    public PlacementObject selected;

    Color selectedColor = new Color(1f, 0.85f, 0.2f); // 노란색

    void Awake()
    {
        Instance = this;
    }

    public void Select(PlacementObject obj)
    {
        Deselect();
        selected = obj;
        SetColor(selected, selectedColor);
    }

    public void Deselect()
    {
        if (selected != null)
            ResetColor(selected);

        selected = null;
    }

    void SetColor(PlacementObject obj, Color color)
    {
        foreach (var sr in obj.GetComponentsInChildren<SpriteRenderer>())
            sr.color = color;
    }

    void ResetColor(PlacementObject obj)
    {
        foreach (var sr in obj.GetComponentsInChildren<SpriteRenderer>())
            sr.color = Color.white; // 기본색
    }

    public void RefreshSelectedColor()
    {
        if (selected == null)
            return;

        SetColor(selected, selectedColor);
    }

}

