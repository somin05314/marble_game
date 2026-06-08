using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneStarter : MonoBehaviour
{
    [SerializeField] string firstStageSceneName = "Stage01";

    void Start()
    {
        // Core만 열린 상태면 첫 스테이지를 로드
        if (SceneManager.GetActiveScene().name == "Core")
        {
            SceneManager.LoadScene(firstStageSceneName);
        }
    }
}
