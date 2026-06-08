using UnityEngine;
using UnityEngine.SceneManagement;

public class StageSelectController : MonoBehaviour
{
    [SerializeField] string defaultStageId = "Stage_03";
    [SerializeField] string stageSceneName = "StageScene";
    [SerializeField] KeyCode enterKey = KeyCode.Return;

    public void EnterStage(string stageId)
    {
        StageContext.SetStageId(stageId);
        UnityEngine.SceneManagement.SceneManager.LoadScene("StageScene");
    }


    void Update()
    {
        if (Input.GetKeyDown(enterKey))
            EnterStage(defaultStageId);
    }
}
