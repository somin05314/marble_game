#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayRedirectToBootstrap : MonoBehaviour
{
    [SerializeField] string bootstrapScene = "Bootstrap";
    [SerializeField] string coreScene = "Core";

    [Header("Redirect Policy")]
    [SerializeField] string stageScenePrefix = "Stage";

    void Awake()
    {
        if (SceneManager.GetSceneByName(coreScene).isLoaded)
            return;

        string requested = gameObject.scene.name;

        if (IsStageScene(requested))
            EditorStartScene.RequestedScene = requested;
        else
            EditorStartScene.RequestedScene = "";

        SceneManager.LoadScene(bootstrapScene);
    }

    bool IsStageScene(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) &&
               !string.IsNullOrEmpty(stageScenePrefix) &&
               sceneName.StartsWith(stageScenePrefix);
    }
}
#endif