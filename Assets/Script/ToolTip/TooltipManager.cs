using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager I { get; private set; }

    [Header("UI")]
    [SerializeField] RectTransform root;
    [SerializeField] TMP_Text label;
    [SerializeField] RectTransform labelRect;

    [Header("Text Size")]
    [Tooltip("툴팁 텍스트 최대 너비. 짧은 텍스트는 내용만큼만, 긴 텍스트는 이 너비에서 줄바꿈됩니다.")]
    [SerializeField] float maxTextWidth = 260f;

    [Header("Canvas (for scaler/positioning)")]
    [Tooltip("툴팁이 속한 Canvas. 비워두면 root의 상위에서 자동 탐색")]
    [SerializeField] Canvas canvas;

    [Header("Behavior")]
    [SerializeField, Range(0f, 1f)] float showDelay = 0.3f;

    [Header("Position")]
    [SerializeField] float marginX = 18f;

    [SerializeField] float marginUp = 30f;     // 위쪽
    [SerializeField] float marginDown = 60f;   // 아래쪽

    [Tooltip("툴팁이 화면 밖으로 나가지 않게 클램프")]
    [SerializeField] bool clampToCanvas = true;

    [Header("Panel Padding")]
    [SerializeField] float paddingLeft = 16f;
    [SerializeField] float paddingRight = 16f;
    [SerializeField] float paddingTop = 10f;
    [SerializeField] float paddingBottom = 10f;

    float _showAt;
    bool _pending;
    string _pendingKey;

    TooltipTrigger.XDir _xDir = TooltipTrigger.XDir.Right;
    TooltipTrigger.YDir _yDir = TooltipTrigger.YDir.Up;

    string _shownKey;

    RectTransform _canvasRt;

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        if (root == null)
        {
            enabled = false;
            return;
        }

        if (labelRect == null && label != null)
            labelRect = label.rectTransform;

        if (canvas == null)
            canvas = root.GetComponentInParent<Canvas>();

        _canvasRt = (canvas != null) ? canvas.GetComponent<RectTransform>() : null;

        DisableRaycast(root);
        HideImmediate();
    }

    void Update()
    {
        if (_pending && Time.unscaledTime >= _showAt)
        {
            ShowKeyNow(_pendingKey);
            _pending = false;
        }

        if (root != null && root.gameObject.activeSelf)
        {
            UpdateTooltipPosition();
        }
    }

    public void RequestShowKey(string key, TooltipTrigger.XDir xDir, TooltipTrigger.YDir yDir)
    {
        key = key?.Trim();
        if (string.IsNullOrEmpty(key)) return;

        if (root != null && root.gameObject.activeSelf && _shownKey == key)
        {
            _xDir = xDir;
            _yDir = yDir;
            return;
        }

        _pendingKey = key;
        _pending = true;
        _showAt = Time.unscaledTime + showDelay;

        _xDir = xDir;
        _yDir = yDir;
    }

    public void Cancel()
    {
        _pending = false;
        _shownKey = null;
        HideImmediate();
    }

    void ShowKeyNow(string key)
    {
        key = key?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            HideImmediate();
            return;
        }

        if (LocalizationManager.I != null && LocalizationManager.I.TryGet(key, out var text))
        {
            if (label != null)
                label.text = text;

            root.gameObject.SetActive(true);

            ApplyLabelSize(text);

            Canvas.ForceUpdateCanvases();

            _shownKey = key;
            UpdateTooltipPosition();
        }
        else
        {
            HideImmediate();
        }
    }
    void ApplyLabelSize(string text)
    {
        if (label == null || labelRect == null || root == null)
            return;

        string safeText = text ?? string.Empty;

        label.enableWordWrapping = true;
        label.ForceMeshUpdate();

        // 1) 한 줄 기준 선호 너비
        Vector2 preferredUnclamped = label.GetPreferredValues(safeText, 10000f, 0f);
        float targetWidth = Mathf.Min(preferredUnclamped.x, maxTextWidth);

        // 2) 그 너비 기준 실제 높이 계산
        Vector2 preferredClamped = label.GetPreferredValues(safeText, targetWidth, 0f);
        float targetHeight = preferredClamped.y;

        // 3) 텍스트 크기 적용
        labelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
        labelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);

        // 4) 패널 크기도 직접 적용
        float panelWidth = targetWidth + paddingLeft + paddingRight;
        float panelHeight = targetHeight + paddingTop + paddingBottom;

        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelWidth);
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight);
    }

    void UpdateTooltipPosition()
    {
        if (_canvasRt == null)
        {
            root.position = (Vector2)Input.mousePosition + ComputeOffsetLocalAsScreenFallback();
            return;
        }

        Camera eventCam = null;
        if (canvas != null && (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace))
            eventCam = canvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, Input.mousePosition, eventCam, out var localMouse))
            return;

        Vector2 offset = ComputeOffsetLocal();
        Vector2 desired = localMouse + offset;

        if (clampToCanvas)
            desired = ClampAnchoredToCanvas(desired);

        root.anchoredPosition = desired;
    }

    Vector2 ComputeOffsetLocal()
    {
        float x = (_xDir == TooltipTrigger.XDir.Right) ? marginX : -marginX;

        float y = (_yDir == TooltipTrigger.YDir.Up)
            ? marginUp
            : -marginDown;
        return new Vector2(x, y);
    }

    Vector2 ComputeOffsetLocalAsScreenFallback()
    {
        float x = (_xDir == TooltipTrigger.XDir.Right) ? marginX : -marginX;

        float y = (_yDir == TooltipTrigger.YDir.Up)
            ? marginUp
            : -marginDown;
        return new Vector2(x, y);
    }

    Vector2 ClampAnchoredToCanvas(Vector2 anchoredPos)
    {
        if (_canvasRt == null || root == null) return anchoredPos;

        Vector2 canvasHalf = _canvasRt.rect.size * 0.5f;
        Vector2 tipSize = root.rect.size;

        float left = -canvasHalf.x + tipSize.x * root.pivot.x;
        float right = canvasHalf.x - tipSize.x * (1f - root.pivot.x);
        float bottom = -canvasHalf.y + tipSize.y * root.pivot.y;
        float top = canvasHalf.y - tipSize.y * (1f - root.pivot.y);

        anchoredPos.x = Mathf.Clamp(anchoredPos.x, left, right);
        anchoredPos.y = Mathf.Clamp(anchoredPos.y, bottom, top);

        return anchoredPos;
    }

    void HideImmediate()
    {
        if (root != null)
            root.gameObject.SetActive(false);
    }

    static void DisableRaycast(RectTransform rt)
    {
        if (rt == null) return;

        var graphics = rt.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;

        var cg = rt.GetComponentInChildren<CanvasGroup>(true);
        if (cg != null) cg.blocksRaycasts = false;
    }

    public void RestartShowKey(string key, TooltipTrigger.XDir xDir, TooltipTrigger.YDir yDir)
    {
        key = key?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            Cancel();
            return;
        }

        // 현재 보이던 / 대기 중이던 툴팁을 닫고
        _pending = false;
        _pendingKey = null;
        _shownKey = null;
        HideImmediate();

        // 다시 딜레이 후 표시되도록 재등록
        _pendingKey = key;
        _pending = true;
        _showAt = Time.unscaledTime + showDelay;

        _xDir = xDir;
        _yDir = yDir;
    }
}