using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LoadingOverlay : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] Image dim;                 // 화면 덮는 이미지
    [SerializeField] Slider progressBar;        // 선택(없어도 됨)

    [Header("Fade")]
    [SerializeField] float fadeDuration = 0.15f;

    void Awake()
    {
        SetBlocking(false);
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    public void SetProgress(float t)
    {
        if (progressBar != null) progressBar.value = Mathf.Clamp01(t);
    }

    void SetBlocking(bool block)
    {
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = block;
            canvasGroup.interactable = block;
        }

        if (dim != null)
            dim.raycastTarget = block;
    }

    // ✅ 비침 0% 핵심: 즉시 완전 검정으로 덮기
    public void ShowBlackImmediate()
    {
        gameObject.SetActive(true);
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        SetBlocking(true);
    }

    public void HideImmediate()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        SetBlocking(false);
        gameObject.SetActive(false);
    }

    public IEnumerator FadeIn()
    {
        gameObject.SetActive(true);
        SetBlocking(true);

        float t = 0f;
        float start = (canvasGroup != null) ? canvasGroup.alpha : 0f;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = (fadeDuration <= 0f) ? 1f : Mathf.Lerp(start, 1f, t / fadeDuration);
            if (canvasGroup != null) canvasGroup.alpha = a;
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    public IEnumerator FadeOut()
    {
        float t = 0f;
        float start = (canvasGroup != null) ? canvasGroup.alpha : 1f;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = (fadeDuration <= 0f) ? 0f : Mathf.Lerp(start, 0f, t / fadeDuration);
            if (canvasGroup != null) canvasGroup.alpha = a;
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        SetBlocking(false);
        gameObject.SetActive(false);
    }
}