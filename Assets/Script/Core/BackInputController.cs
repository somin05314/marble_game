using UnityEngine;

public class BackInputController : MonoBehaviour
{
    [SerializeField] KeyCode backKey = KeyCode.Escape;
    [SerializeField] TutorialPanelController tutorialPanelController;

    void Update()
    {
        if (!Input.GetKeyDown(backKey))
            return;

        // 1. 옵션 패널이 열려 있으면 먼저 닫기
        if (OptionsPanelController.I != null && OptionsPanelController.I.IsOpen)
        {
            OptionsPanelController.I.Close();
            return;
        }

        // 2. 튜토리얼 패널이 열려 있으면 먼저 닫기
        if (tutorialPanelController != null && tutorialPanelController.IsOpen)
        {
            tutorialPanelController.CloseTutorial();
            return;
        }

        // 3. 아무 패널도 안 열려 있으면 뒤로 가기
        if (SceneFlow.I != null)
            SceneFlow.I.GoBack();
    }
}