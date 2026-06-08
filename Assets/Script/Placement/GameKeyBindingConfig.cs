using UnityEngine;

[CreateAssetMenu(menuName = "Game/Input/Game Key Binding Config")]
public class GameKeyBindingConfig : ScriptableObject
{
    [Header("PO Placement")]
    public string placeFlipX = "X";
    public string placeStrengthUp = "C";
    public string placeStrengthDown = "V";
    public string placeCancel = "B";

    [Header("Selected PO Actions")]
    public string demo = "Z";
    public string selectedFlipX = "X";
    public string selectedStrengthDown = "C";
    public string selectedStrengthUp = "V";
    public string selectedDelete = "B";

    [Header("Rail Actions")]
    public string railDelete = "B";

    public bool GetKeyDown(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (!System.Enum.TryParse(key, true, out KeyCode keyCode))
            return false;

        return Input.GetKeyDown(keyCode);
    }

    public bool GetKeyUp(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (!System.Enum.TryParse(key, true, out KeyCode keyCode))
            return false;

        return Input.GetKeyUp(keyCode);
    }

    public KeyCode ToKeyCode(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return KeyCode.None;

        if (System.Enum.TryParse(key, true, out KeyCode keyCode))
            return keyCode;

        return KeyCode.None;
    }
}