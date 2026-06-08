using UnityEngine;

public static class GoalHitFeedbackOption
{
    const string Key = "Option_GoalHitFeedback";
    const bool DefaultValue = true;

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(Key, DefaultValue ? 1 : 0) == 1;
        set
        {
            PlayerPrefs.SetInt(Key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}