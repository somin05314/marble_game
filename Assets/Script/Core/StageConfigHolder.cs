using UnityEngine;

public class StageConfigHolder : MonoBehaviour
{
    public StageConfig config;

    [Header("Scene-only refs")]
    public Transform fixedRoot; // ✅ 이 씬에서 고정 PO들이 들어있는 루트
}
