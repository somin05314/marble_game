using UnityEngine;

public class CursorManager : MonoBehaviour
{
    public static CursorManager I;

    [System.Serializable]
    public class CursorSizeSet
    {
        [Header("Textures")]
        public Texture2D small;
        public Texture2D medium;
        public Texture2D large;

        [Header("Hotspots (px)")]
        public Vector2 hotspotSmall = Vector2.zero;
        public Vector2 hotspotMedium = Vector2.zero;
        public Vector2 hotspotLarge = Vector2.zero;

        public Texture2D GetTexture(CursorSize size)
        {
            switch (size)
            {
                case CursorSize.Small: return small;
                case CursorSize.Medium: return medium;
                case CursorSize.Large: return large;
                default: return medium;
            }
        }

        public Vector2 GetHotspot(CursorSize size)
        {
            switch (size)
            {
                case CursorSize.Small: return hotspotSmall;
                case CursorSize.Medium: return hotspotMedium;
                case CursorSize.Large: return hotspotLarge;
                default: return hotspotMedium;
            }
        }
    }

    public enum CursorState
    {
        Default,
        Hand,
        Cross
    }

    public enum CursorSize
    {
        Small = 0,
        Medium = 1,
        Large = 2
    }

    [Header("Cursor Sets")]
    [SerializeField] CursorSizeSet defaultCursor;
    [SerializeField] CursorSizeSet handCursor;
    [SerializeField] CursorSizeSet crossCursor;

    [Header("Default Size")]
    [SerializeField] CursorSize defaultSize = CursorSize.Medium;

    const string CursorSizeKey = "Cursor_Size";

    CursorState _currentState = CursorState.Default;
    CursorSize _currentSize = CursorSize.Medium;

    public CursorSize CurrentSize => _currentSize;
    public CursorState CurrentState => _currentState;

    void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        LoadCursorSize();
        ApplyCursor(_currentState, true);
    }

    public void Set(CursorState state)
    {
        ApplyCursor(state, false);
    }

    public void SetSize(CursorSize size)
    {
        if (_currentSize == size)
            return;

        _currentSize = size;
        SaveCursorSize();
        ApplyCursor(_currentState, true);
    }

    public void SetSizeSmall()
    {
        SetSize(CursorSize.Small);
    }

    public void SetSizeMedium()
    {
        SetSize(CursorSize.Medium);
    }

    public void SetSizeLarge()
    {
        SetSize(CursorSize.Large);
    }

    void ApplyCursor(CursorState state, bool force)
    {
        if (!force && _currentState == state)
            return;

        _currentState = state;

        CursorSizeSet set = GetCursorSet(state);
        if (set == null)
            return;

        Texture2D tex = set.GetTexture(_currentSize);
        Vector2 hotspot = set.GetHotspot(_currentSize);

        Cursor.SetCursor(tex, hotspot, CursorMode.Auto);
    }

    CursorSizeSet GetCursorSet(CursorState state)
    {
        switch (state)
        {
            case CursorState.Default: return defaultCursor;
            case CursorState.Hand: return handCursor;
            case CursorState.Cross: return crossCursor;
            default: return defaultCursor;
        }
    }

    void SaveCursorSize()
    {
        PlayerPrefs.SetInt(CursorSizeKey, (int)_currentSize);
        PlayerPrefs.Save();
    }

    void LoadCursorSize()
    {
        int saved = PlayerPrefs.GetInt(CursorSizeKey, (int)defaultSize);

        if (saved < (int)CursorSize.Small || saved > (int)CursorSize.Large)
            saved = (int)defaultSize;

        _currentSize = (CursorSize)saved;
    }
}