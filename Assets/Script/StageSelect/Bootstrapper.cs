using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Bootstrapper : MonoBehaviour
{
    [SerializeField] string coreScene = "Core";
    [SerializeField] string defaultFirstContent = "StageSelect";

    IEnumerator Start()
    {
        // 1) Core 보장
        if (!SceneManager.GetSceneByName(coreScene).isLoaded)
            yield return SceneManager.LoadSceneAsync(coreScene, LoadSceneMode.Additive);

        // 2) 처음 열 컨텐츠 결정
        string firstContent = defaultFirstContent;

#if UNITY_EDITOR
        // ✅ 에디터에서 Stage 씬에서 Play한 경우
        if (!string.IsNullOrEmpty(EditorStartScene.RequestedScene))
        {
            firstContent = EditorStartScene.RequestedScene;
            EditorStartScene.RequestedScene = null; // 한 번 쓰고 비우기
        }
#endif

        // 3) 컨텐츠 씬 로드
        Scene targetScene = SceneManager.GetSceneByName(firstContent);
        if (!targetScene.IsValid() || !targetScene.isLoaded)
            yield return SceneManager.LoadSceneAsync(firstContent, LoadSceneMode.Additive);

        targetScene = SceneManager.GetSceneByName(firstContent);

        // 4) 활성 씬 설정
        if (targetScene.IsValid() && targetScene.isLoaded)
            SceneManager.SetActiveScene(targetScene);
        else
            Debug.LogError($"[Bootstrapper] Failed to load first content scene: {firstContent}");

        // 5) Bootstrap 씬 언로드
        yield return SceneManager.UnloadSceneAsync(gameObject.scene);
    }
}