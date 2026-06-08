using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameFlowController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] GameModeManager gameMode;
    [SerializeField] BuildToolManager toolManager;
    [SerializeField] GridPlacer gridPlacer;

    [Header("Stage")]
    [SerializeField] StageConfig stageConfig;

    [Header("Input")]
    [SerializeField] bool useHotkeys = true;
    [SerializeField] float rightClickDragThresholdPx = 6f;

    [Header("Save/Load")]
    [SerializeField] StageSaveManager stageSave;
    [SerializeField] PuzzleSnapshotManager puzzleSnapshot;
    [Header("UI - Play Toggle")]
    [SerializeField] Button playToggleButton;
    [SerializeField] Image playToggleIcon;
    [SerializeField] Sprite spritePlay;           // Build일 때 보여줄 ▶
    [SerializeField] Sprite spriteResetToBuild;   // Play일 때 보여줄 ↺(Build로/리셋)

    [Header("UI - Play Toggle Tooltip")]
    [SerializeField] TooltipTrigger playToggleTooltip;
    [SerializeField] string tooltipKeyPlay = "tooltip_play";
    [SerializeField] string tooltipKeyResetToBuild = "tooltip_reset_to_build";

    [Header("UI - Hint")]
    [SerializeField] HintButtonUI hintButtonUI;

    Vector3 _rcDownScreen;
    bool _rcHeld;
    bool _rcDragged;

    bool _pendingEnterBuildAfterRestore;
    bool _blockModeToggleInput;

    int _lastStageSceneHandle = -1;

    void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;

        GameModeManager.OnModeChanged += HandleModeChanged;

        if (puzzleSnapshot == null)
            puzzleSnapshot = FindFirstObjectByType<PuzzleSnapshotManager>();

        if (puzzleSnapshot != null)
            puzzleSnapshot.OnRestoreCompleted += HandlePuzzleRestoreCompleted;
    }

    void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;

        GameModeManager.OnModeChanged -= HandleModeChanged;

        if (puzzleSnapshot != null)
            puzzleSnapshot.OnRestoreCompleted -= HandlePuzzleRestoreCompleted;
    }

    void HandleModeChanged(GameMode m)
    {
        UpdatePlayToggleVisual();
    }

    void Awake()
    {
        if (gameMode == null) gameMode = GameModeManager.Instance ?? FindFirstObjectByType<GameModeManager>();
        if (toolManager == null) toolManager = BuildToolManager.Instance ?? FindFirstObjectByType<BuildToolManager>();
        if (gridPlacer == null) gridPlacer = FindFirstObjectByType<GridPlacer>();
        if (stageSave == null) stageSave = FindFirstObjectByType<StageSaveManager>();
        if (puzzleSnapshot == null) puzzleSnapshot = FindFirstObjectByType<PuzzleSnapshotManager>();
        if (hintButtonUI == null) hintButtonUI = FindFirstObjectByType<HintButtonUI>();

        if (playToggleTooltip == null && playToggleButton != null)
            playToggleTooltip = playToggleButton.GetComponent<TooltipTrigger>();

        if (playToggleButton != null)
        {
            playToggleButton.onClick.RemoveListener(BtnPlayToggle);
            playToggleButton.onClick.AddListener(BtnPlayToggle);
        }

        RefreshStageContext(force: true);
        UpdatePlayToggleVisual();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshStageContext(force: false);
    void OnActiveSceneChanged(Scene prev, Scene next) => RefreshStageContext(force: true);

    // =========================
    // UI Buttons: Save / Load / Delete
    // =========================
    public void BtnSaveStage()
    {
        if (stageSave == null) { Debug.LogWarning("[GameFlow] StageSaveManager not found."); return; }
        stageSave.SaveCurrentStage();
    }

    public void BtnLoadStage()
    {
        if (stageSave == null) { Debug.LogWarning("[GameFlow] StageSaveManager not found."); return; }
        stageSave.LoadCurrentStage();
    }

    public void BtnDeleteStageSave()
    {
        if (stageSave == null) { Debug.LogWarning("[GameFlow] StageSaveManager not found."); return; }
        stageSave.DeleteCurrentSave();
    }

    public void BtnPlayToggle()
    {
        if (gameMode == null) return;
        if (IsModeToggleBlockedNow()) return;

        if (gameMode.IsBuildMode)
        {
            // 플레이 진입 직전 현재 상태를 스냅샷 저장
            if (puzzleSnapshot != null)
                puzzleSnapshot.Save();

            if (UISoundManager.I != null)
                UISoundManager.I.PlayApply();

            CmdEnterPlayMode();
        }
        else
        {
            if (UISoundManager.I != null)
                UISoundManager.I.PlayRelease();

            // 플레이 -> 빌드는 즉시 전환하지 말고
            // 스냅샷 복원 완료 후 BuildMode 진입
            if (puzzleSnapshot != null)
            {
                _blockModeToggleInput = true;
                _pendingEnterBuildAfterRestore = true;
                puzzleSnapshot.RestoreNow();
            }
            else
            {
                // 스냅샷 매니저가 없으면 fallback
                CmdEnterBuildMode();
            }
        }
    }

    void UpdatePlayToggleVisual()
    {
        if (gameMode == null) return;

        bool isBuild = gameMode.IsBuildMode;

        if (playToggleIcon != null)
            playToggleIcon.sprite = isBuild ? spritePlay : spriteResetToBuild;

        if (playToggleTooltip != null)
        {
            playToggleTooltip.tooltipKey = isBuild ? tooltipKeyPlay : tooltipKeyResetToBuild;

            // 버튼 위에 마우스가 남아있을 때만
            // 툴팁을 닫고 showDelay 후 다시 표시
            if (IsPointerOverPlayToggleButton())
            {
                TooltipManager.I?.RestartShowKey(
                    playToggleTooltip.tooltipKey,
                    playToggleTooltip.xDir,
                    playToggleTooltip.yDir
                );
            }
            else
            {
                // 마우스가 버튼 위에 없으면 굳이 툴팁 재표시 안 함
                TooltipManager.I?.Cancel();
            }
        }
    }
    void RefreshStageContext(bool force)
    {
        var active = SceneManager.GetActiveScene();

        if (gameMode == null) gameMode = GameModeManager.Instance ?? FindFirstObjectByType<GameModeManager>();
        if (toolManager == null) toolManager = BuildToolManager.Instance ?? FindFirstObjectByType<BuildToolManager>();

        if (gridPlacer == null || gridPlacer.gameObject.scene != active)
            gridPlacer = FindFirstObjectByType<GridPlacer>();

        StageConfigHolder holder = null;
        var holders = FindObjectsByType<StageConfigHolder>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < holders.Length; i++)
        {
            if (holders[i] != null && holders[i].gameObject.scene == active)
            {
                holder = holders[i];
                break;
            }
        }

        var nextConfig = (holder != null) ? holder.config : null;
        if (force || stageConfig != nextConfig)
            stageConfig = nextConfig;

        if (holder != null && gameMode != null)
        {
            gameMode.EnsureBuildModeForStageEntry();
        }

        if (gridPlacer != null)
        {
            if (holder != null) gridPlacer.SetFixedRoot(holder.fixedRoot);
            else gridPlacer.SetFixedRoot(null);

            if (gridPlacer.gameObject.scene == active && _lastStageSceneHandle != active.handle)
            {
                _lastStageSceneHandle = active.handle;

                gridPlacer.SetPlacementData(null);

                if (toolManager != null)
                {
                    if (gameMode != null && gameMode.IsBuildMode)
                        toolManager.SetTool(BuildTool.Select);
                    else
                        toolManager.SetTool(BuildTool.None);
                }
            }
        }

        UpdatePlayToggleVisual();
    }

    void Update()
    {
        if (!useHotkeys) return;
        if (gameMode == null) return;
        if (IsModeToggleBlockedNow()) return;

        // 🔥 Space로 Build <-> Play 전환
        if (Input.GetKeyDown(KeyCode.Space))
        {
            BtnPlayToggle();
            return;
        }

        if (toolManager == null) return;

        if (!gameMode.IsBuildMode) return;

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) CmdSelectPlacementFromStage(0, true);
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) CmdSelectPlacementFromStage(1, true);
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) CmdSelectPlacementFromStage(2, true);
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) CmdSelectPlacementFromStage(3, true);
        if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) CmdSelectPlacementFromStage(4, true);
        if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) CmdSelectPlacementFromStage(5, true);
        if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) CmdSelectPlacementFromStage(6, true);
        if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) CmdSelectPlacementFromStage(7, true);
        if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) CmdSelectPlacementFromStage(8, true);
        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) CmdSelectPlacementFromStage(9, true);

        HandleRightClickSelect();
    }

    void HandleRightClickSelect()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetMouseButtonDown(1))
        {
            _rcHeld = true;
            _rcDragged = false;
            _rcDownScreen = Input.mousePosition;
        }

        if (_rcHeld && Input.GetMouseButton(1))
        {
            Vector3 cur = Input.mousePosition;
            float dist2 = (cur - _rcDownScreen).sqrMagnitude;

            float th = rightClickDragThresholdPx;
            if (!_rcDragged && dist2 >= th * th)
                _rcDragged = true;
        }

        if (_rcHeld && Input.GetMouseButtonUp(1))
        {
            bool wasDragged = _rcDragged;

            _rcHeld = false;
            _rcDragged = false;

            if (!wasDragged)
                CmdSetTool(BuildTool.Select);
        }
    }

    public void CmdSelectPlacementFromStage(int index, bool forcePlaceTool = true)
    {
        if (!gameMode.IsBuildMode) return;
        if (stageConfig == null) return;

        var list = stageConfig.allowedPlacements;
        if (list == null || list.Length == 0) return;
        if (index < 0 || index >= list.Length) return;

        PlacementData data = list[index];

        if (IsPlacementLimitReached(data))
        {
            Debug.Log($"[GameFlow] Placement limit reached: {data.name}");
            return;
        }

        CmdSelectPlacement(data, forcePlaceTool);
    }

    public void CmdSelectPlacement(PlacementData data, bool forcePlaceTool = true)
    {
        if (!gameMode.IsBuildMode) return;
        if (data == null) return;

        if (forcePlaceTool)
            toolManager.SetTool(BuildTool.Place);

        if (gridPlacer != null)
            gridPlacer.SetPlacementData(data);
    }

    public void CmdEnterPlayMode()
    {
        if (gameMode == null) return;
        if (IsModeToggleBlockedNow()) return;

        gameMode.EnterPlayMode();

        if (toolManager != null)
            toolManager.SetTool(BuildTool.None);

        if (hintButtonUI != null)
            hintButtonUI.OnEnterPlayMode();
    }

    public void CmdEnterBuildMode()
    {
        if (gameMode == null) return;

        gameMode.EnterBuildMode();

        if (toolManager != null)
            toolManager.SetTool(BuildTool.Select);

        if (hintButtonUI != null)
            hintButtonUI.OnEnterBuildMode();
    }

    public void CmdSetTool(BuildTool tool)
    {
        if (!gameMode.IsBuildMode) return;
        toolManager.SetTool(tool);
    }

    public void BtnToolPlace() => CmdSetTool(BuildTool.Place);
    public void BtnToolSelect() => CmdSetTool(BuildTool.Select);

    bool IsPointerOverPlayToggleButton()
    {
        if (playToggleButton == null)
            return false;

        var rt = playToggleButton.transform as RectTransform;
        if (rt == null)
            return false;

        Canvas canvas = playToggleButton.GetComponentInParent<Canvas>();
        Camera eventCam = null;

        if (canvas != null &&
            (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace))
        {
            eventCam = canvas.worldCamera;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, eventCam);
    }

    void HandlePuzzleRestoreCompleted()
    {
        _blockModeToggleInput = false;

        if (!_pendingEnterBuildAfterRestore)
            return;

        _pendingEnterBuildAfterRestore = false;
        CmdEnterBuildMode();
    }

    bool IsModeToggleBlockedNow()
    {
        if (_blockModeToggleInput)
            return true;

        // PuzzleSnapshot 복원 중
        if (puzzleSnapshot != null && puzzleSnapshot.IsRestoring)
            return true;

        // StageSaveManager 복원/언도/복원 안정화 중
        if (StageSaveManager.IsRestoringNow || StageSaveManager.IsRestoreStabilizingNow)
            return true;

        return false;
    }

    bool IsPlacementLimitReached(PlacementData data)
    {
        if (stageConfig == null || data == null)
            return false;

        int maxCount = stageConfig.GetMaxCount(data);

        // 0이면 무제한
        if (maxCount <= 0)
            return false;

        var reg = StageObjectRegistry.Instance;
        if (reg == null)
            return false;

        reg.CleanupNulls();

        int placedCount = 0;
        var activeScene = SceneManager.GetActiveScene();
        var ghostLayer = LayerMask.NameToLayer("Ghost");

        var objects = reg.PlacementObjects;

        for (int i = 0; i < objects.Count; i++)
        {
            var po = objects[i];
            if (po == null)
                continue;

            if (po.gameObject.scene != activeScene)
                continue;

            if (!po.gameObject.activeInHierarchy)
                continue;

            // StagePlacementPaletteUI와 동일하게 Ghost 제외
            if (po.gameObject.layer == ghostLayer)
                continue;

            // StagePlacementPaletteUI와 동일하게 FixedRoot 제외
            if (IsUnderFixedRoot(po.transform))
                continue;

            if (po.placementData == data)
                placedCount++;
        }

        Debug.Log($"[GameFlow] {data.name} count {placedCount} / {maxCount}");

        return placedCount >= maxCount;
    }

    [SerializeField] string fixedRootName = "FixedRoot";

    bool IsUnderFixedRoot(Transform t)
    {
        if (t == null) return false;
        if (string.IsNullOrEmpty(fixedRootName)) return false;

        while (t != null)
        {
            if (t.name == fixedRootName)
                return true;

            t = t.parent;
        }

        return false;
    }
}