using UnityEngine;
using UnityEngine.SceneManagement;

public class GlobalRailCleaner : MonoBehaviour
{
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ClearAllRailCaches();
    }

    void ClearAllRailCaches()
    {
        RailEdgeRegistry2D.ClearAll();
    }
}
