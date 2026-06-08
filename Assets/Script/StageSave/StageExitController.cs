using UnityEngine;

public class StageExitController : MonoBehaviour
{
    [SerializeField] KeyCode backKey = KeyCode.Escape;

    void Update()
    {
        if (!Input.GetKeyDown(backKey)) return;

        if (SceneFlow.I != null)
            SceneFlow.I.GoBack();
    }

    // UI 버튼용
    public void OnClickBack()
    {
        if (SceneFlow.I != null)
            SceneFlow.I.GoBack();
    }
}