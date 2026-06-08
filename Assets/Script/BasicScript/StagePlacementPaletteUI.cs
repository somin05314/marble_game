using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StagePlacementPaletteUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] GameFlowController flow;
    [SerializeField] StageConfigHolder holder;
    [SerializeField] Transform buttonRoot;
    [SerializeField] Button buttonPrefab;

    [Header("Options")]
    [SerializeField] bool autoSelectDefaultOnStart = true;

    [Header("Count UI")]
    [SerializeField] bool showCountOnButton = true;
    [SerializeField] bool disableWhenExhausted = true;
    [SerializeField] float autoRefreshInterval = 0.25f;

    [Header("Exclude")]
    [SerializeField] string fixedRootName = "FixedRoot";

    [Header("Icon (Prefab Toggle)")]
    [Tooltip("버튼 아래에 아이콘을 붙일 부모(없으면 버튼 transform)")]
    [SerializeField] string iconRootChildName = "IconRoot";

    [Header("Shortcut Number UI")]
    [SerializeField] bool showShortcutNumber = true;
    [SerializeField] string shortcutTextChildName = "ShortcutText";

    TMP_Text[] _shortcutTexts;

    Button[] _buttons;
    TMP_Text[] _texts; // ✅ TMP로 변경

    // 버튼별 아이콘 인스턴스 캐싱
    GameObject[] _iconNormalGO;
    GameObject[] _iconExhaustedGO;

    int[] _lastCurCounts; // ✅ 마지막으로 계산된 현재 배치 수(allowedPlacements 인덱스 기준)


    float _nextRefreshTime;

    void Awake()
    {
        if (flow == null) flow = FindFirstObjectByType<GameFlowController>();
        if (holder == null) holder = FindFirstObjectByType<StageConfigHolder>();
    }

    void Start()
    {
        BuildButtons();
        RefreshCounts();
    }

    void Update()
    {
        if (autoRefreshInterval <= 0f) return;
        if (Time.unscaledTime < _nextRefreshTime) return;
        _nextRefreshTime = Time.unscaledTime + autoRefreshInterval;

        RefreshCounts();
    }

    public void BuildButtons()
    {
        if (buttonRoot == null || buttonPrefab == null) return;
        if (holder == null || holder.config == null) return;

        var stage = holder.config;
        var list = stage.allowedPlacements;
        if (list == null) return;

        // 기존 버튼 제거
        for (int i = buttonRoot.childCount - 1; i >= 0; i--)
            Destroy(buttonRoot.GetChild(i).gameObject);

        int n = list.Length;

        _buttons = new Button[n];
        _texts = new TMP_Text[n]; // ✅ TMP로 변경

        _iconNormalGO = new GameObject[n];
        _iconExhaustedGO = new GameObject[n];

        _shortcutTexts = new TMP_Text[n];

        for (int i = 0; i < n; i++)
        {
            int index = i;
            var pd = list[i];

            var btn = Instantiate(buttonPrefab, buttonRoot);
            _buttons[i] = btn;

            if (showShortcutNumber)
            {
                Transform shortcutTr = btn.transform.Find(shortcutTextChildName);
                TMP_Text shortcutTxt = shortcutTr != null
                    ? shortcutTr.GetComponent<TMP_Text>()
                    : null;

                _shortcutTexts[i] = shortcutTxt;

                if (shortcutTxt != null)
                    shortcutTxt.text = $"[{i + 1}]";
            }

            var txt = btn.GetComponentInChildren<TMP_Text>(true);
            _texts[i] = txt;

            if (txt != null)
            {
                txt.enableWordWrapping = false;
                txt.enableAutoSizing = false;
                txt.overflowMode = TextOverflowModes.Overflow; // or Ellipsis
                txt.text = "";
            }



            // 아이콘 붙일 부모 찾기
            Transform iconParent = FindIconRoot(btn.transform);

            // ✅ PlacementData에 들어있는 프리팹으로 아이콘 생성
            GameObject normalPrefab = (pd != null) ? pd.iconNormalUIPrefab : null;
            GameObject exhaustedPrefab = (pd != null) ? pd.iconExhaustedUIPrefab : null;

            _iconNormalGO[i] = InstantiateIf(normalPrefab, iconParent);
            _iconExhaustedGO[i] = InstantiateIf(exhaustedPrefab, iconParent);

            ApplyIconTransform(_iconNormalGO[i], pd);
            ApplyIconTransform(_iconExhaustedGO[i], pd);

            // 최초 상태:
            // - normal이 있으면 normal ON
            // - normal이 없고 exhausted만 있으면 exhausted ON
            bool hasNormal = _iconNormalGO[i] != null;
            bool hasExhaust = _iconExhaustedGO[i] != null;
            SetIconState(i, normal: hasNormal, exhausted: !hasNormal && hasExhaust);

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (disableWhenExhausted && !CanSelect(index))
                    return;

                flow?.CmdSelectPlacementFromStage(index, forcePlaceTool: true);
            });
        }

        RefreshCounts();
    }

    Transform FindIconRoot(Transform buttonTransform)
    {
        if (buttonTransform == null) return null;
        if (string.IsNullOrEmpty(iconRootChildName)) return buttonTransform;

        var child = buttonTransform.Find(iconRootChildName);
        return (child != null) ? child : buttonTransform;
    }

    GameObject InstantiateIf(GameObject prefab, Transform parent)
    {
        if (prefab == null || parent == null) return null;
        var go = Instantiate(prefab, parent);
        go.SetActive(false); // RefreshCounts에서 켤 거라 일단 꺼둠
        return go;
    }

    void SetIconState(int index, bool normal, bool exhausted)
    {
        if (_iconNormalGO != null && index < _iconNormalGO.Length && _iconNormalGO[index] != null)
            _iconNormalGO[index].SetActive(normal);

        if (_iconExhaustedGO != null && index < _iconExhaustedGO.Length && _iconExhaustedGO[index] != null)
            _iconExhaustedGO[index].SetActive(exhausted);
    }

    bool IsUnderFixedRoot(Transform t)
    {
        if (t == null) return false;
        if (string.IsNullOrEmpty(fixedRootName)) return false;

        while (t != null)
        {
            if (t.name == fixedRootName) return true;
            t = t.parent;
        }
        return false;
    }

    public void RefreshCounts()
    {
        if (!showCountOnButton) return;
        if (_buttons == null || _texts == null) return;
        if (holder == null || holder.config == null) return;

        var stage = holder.config;
        var list = stage.allowedPlacements;
        if (list == null) return;
        if (list.Length == 0) return;

        var ghostLayer = LayerMask.NameToLayer("Ghost");

        var placed = GetRegisteredPlacementObjects();
        int[] curCounts = new int[list.Length];

        for (int i = 0; i < placed.Count; i++)
        {
            var po = placed[i];
            if (po == null) continue;
            if (po.gameObject.layer == ghostLayer) continue;
            if (IsUnderFixedRoot(po.transform)) continue;

            var pd = po.placementData;
            if (pd == null) continue;

            for (int k = 0; k < list.Length; k++)
            {
                if (list[k] == pd)
                {
                    curCounts[k]++;
                    break;
                }
            }
        }

        // ✅ 여기로 이동
        _lastCurCounts = curCounts;


        // 버튼 UI 적용
        for (int i = 0; i < list.Length; i++)
        {
            var pd = list[i];
            var btn = _buttons[i];
            var txt = _texts[i];

            int max = stage != null ? stage.GetMaxCount(pd) : 0; // 0이면 무제한
            int cur = curCounts[i];

            bool unlimited = (max <= 0);
            bool exhausted = (!unlimited && cur >= max);

            // ✅ TMP 텍스트
            if (txt != null)
            {
                txt.text = unlimited
                    ? $"{cur}/∞"
                    : $"{cur}/{max}";
            }

            // 버튼 비활성화
            if (btn != null && disableWhenExhausted)
                btn.interactable = !exhausted;

            // 아이콘 토글
            bool hasExhaustIcon = (_iconExhaustedGO != null && i < _iconExhaustedGO.Length && _iconExhaustedGO[i] != null);
            if (exhausted && hasExhaustIcon)
                SetIconState(i, normal: false, exhausted: true);
            else
                SetIconState(i, normal: true, exhausted: false);
        }
    }

    bool CanSelect(int index)
    {
        if (holder == null || holder.config == null) return true;

        var stage = holder.config;
        var list = stage.allowedPlacements;
        if (list == null || index < 0 || index >= list.Length) return true;

        var pd = list[index];
        int max = stage.GetMaxCount(pd);
        if (max <= 0) return true;

        // ✅ RefreshCounts에서 계산한 값을 우선 사용
        if (_lastCurCounts != null && index < _lastCurCounts.Length)
            return _lastCurCounts[index] < max;

        // ✅ 캐시가 아직 없으면(아주 초기 프레임 등) 기존 방식으로 fallback
        var ghostLayer = LayerMask.NameToLayer("Ghost");

        int cur = 0;
        var placed = GetRegisteredPlacementObjects();
        for (int i = 0; i < placed.Count; i++)
        {
            var po = placed[i];
            if (po == null) continue;
            if (po.gameObject.layer == ghostLayer) continue;
            if (IsUnderFixedRoot(po.transform)) continue;

            if (po.placementData == pd) cur++;
        }
        return cur < max;
    }

    void ApplyIconTransform(GameObject iconGO, PlacementData pd)
    {
        if (iconGO == null || pd == null) return;

        // 1) UI 오브젝트라면 RectTransform 크기 적용
        var rt = iconGO.GetComponent<RectTransform>();
        if (rt != null)
        {
            if (pd.useCustomPaletteIconSize)
            {
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, pd.paletteIconSize.x);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, pd.paletteIconSize.y);
            }

            if (pd.useCustomPaletteIconScale)
            {
                rt.localScale = pd.paletteIconScale;
            }

            return;
        }

        // 2) RectTransform이 없으면 일반 Transform scale 적용
        if (pd.useCustomPaletteIconScale)
        {
            iconGO.transform.localScale = pd.paletteIconScale;
        }
    }

    System.Collections.Generic.List<PlacementObject> GetRegisteredPlacementObjects(bool includeFallback = true)
    {
        var result = new System.Collections.Generic.List<PlacementObject>(128);

        var registry = StageObjectRegistry.Instance;
        if (registry != null)
        {
            registry.CleanupNulls();

            var list = registry.PlacementObjects;
            for (int i = 0; i < list.Count; i++)
            {
                var po = list[i];
                if (po == null) continue;
                result.Add(po);
            }

            return result;
        }

        if (includeFallback)
        {
            var found = FindObjectsOfType<PlacementObject>();
            for (int i = 0; i < found.Length; i++)
            {
                var po = found[i];
                if (po == null) continue;
                result.Add(po);
            }
        }

        return result;
    }
}
