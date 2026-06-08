using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CoreHudVisibility : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] GameObject hudRoot; // 비우면 자기 자신

    [Header("Hide On These Scenes")]
    [SerializeField] string[] hideOnScenes = { "StartScene" };

    void Awake()
    {
        if (hudRoot == null) hudRoot = gameObject;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    void Start()
    {
        Apply(SceneManager.GetActiveScene().name);
    }

    void OnActiveSceneChanged(Scene prev, Scene next)
    {
        Apply(next.name);
    }

    void Apply(string activeSceneName)
    {
        bool hide = false;

        if (hideOnScenes != null)
        {
            for (int i = 0; i < hideOnScenes.Length; i++)
            {
                var s = hideOnScenes[i];
                if (string.IsNullOrEmpty(s)) continue;

                if (string.Equals(s, activeSceneName, StringComparison.OrdinalIgnoreCase))
                {
                    hide = true;
                    break;
                }
            }
        }

        hudRoot.SetActive(!hide);
    }
}