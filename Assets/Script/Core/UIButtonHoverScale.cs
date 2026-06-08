using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] RectTransform target;   // 비우면 자기 자신
    [SerializeField] float hoverScale = 1.08f;
    [SerializeField] float pressScale = 0.95f;
    [SerializeField] float speed = 18f;

    [Header("Sound")]
    [SerializeField] bool playHoverSound = true;
    [SerializeField] float hoverSoundCooldown = 0.08f; // 너무 짧게 연속 재생 방지

    Vector3 _baseScale;
    Vector3 _goalScale;
    bool _hover;
    bool _press;

    float _lastHoverSoundTime = -999f;

    void Awake()
    {
        if (target == null) target = (RectTransform)transform;
        _baseScale = target.localScale;
        _goalScale = _baseScale;
    }

    void Update()
    {
        if (target == null) return;

        target.localScale = Vector3.Lerp(
            target.localScale,
            _goalScale,
            1f - Mathf.Exp(-speed * Time.unscaledDeltaTime)
        );
    }

    void RefreshGoal()
    {
        float s = 1f;
        if (_hover) s *= hoverScale;
        if (_press) s *= pressScale;
        _goalScale = _baseScale * s;
    }

    void TryPlayHoverSound()
    {
        if (!playHoverSound) return;
        if (UISoundManager.I == null) return;

        if (Time.unscaledTime - _lastHoverSoundTime < hoverSoundCooldown)
            return;

        _lastHoverSoundTime = Time.unscaledTime;
        UISoundManager.I.PlayHover();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hover = true;
        RefreshGoal();
        TryPlayHoverSound();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hover = false;
        _press = false;
        RefreshGoal();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _press = true;
        RefreshGoal();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _press = false;
        RefreshGoal();
    }

    void OnDisable()
    {
        _hover = false;
        _press = false;

        if (target != null)
        {
            _goalScale = _baseScale;
            target.localScale = _baseScale;
        }
    }

    void OnEnable()
    {
        if (target == null)
            target = (RectTransform)transform;

        _baseScale = target.localScale;
        _goalScale = _baseScale;

        _hover = false;
        _press = false;

        target.localScale = _baseScale;
    }
}