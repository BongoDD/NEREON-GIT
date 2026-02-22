using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

#if DOTWEEN
using DG.Tweening;
#endif

/// <summary>
/// NEREON UI animation helpers.
/// Works fully with DOTween Pro when imported.  Falls back to coroutine-based
/// animations when DOTween is not yet in the project.
///
/// HOW TO ENABLE DOTWEEN:
///   After importing DOTween Pro from the Asset Store, run its Setup wizard.
///   Then open  Edit → Project Settings → Player → Other Settings → Scripting
///   Define Symbols  and add  DOTWEEN  to the list.
///   All animations will then use DOTween's optimised tweening engine.
/// </summary>
public static class UIAnimations
{
    // ─── Fade ────────────────────────────────────────────────────────────────

    /// <summary>Fade a CanvasGroup from its current alpha to <paramref name="targetAlpha"/>.</summary>
    public static async UniTask FadeAsync(CanvasGroup group, float targetAlpha,
                                          float duration = 0.35f)
    {
#if DOTWEEN
        await group.DOFade(targetAlpha, duration).SetEase(Ease.OutQuad).AsyncWaitForCompletion();
#else
        await LerpAlphaAsync(group, targetAlpha, duration);
#endif
    }

    public static UniTask FadeInAsync(CanvasGroup group, float duration = 0.35f)
    {
        group.interactable  = true;
        group.blocksRaycasts = true;
        return FadeAsync(group, 1f, duration);
    }

    public static async UniTask FadeOutAsync(CanvasGroup group, float duration = 0.25f)
    {
        await FadeAsync(group, 0f, duration);
        group.interactable  = false;
        group.blocksRaycasts = false;
    }

    // ─── Slide ───────────────────────────────────────────────────────────────

    public enum SlideDirection { FromBottom, FromTop, FromLeft, FromRight }

    /// <summary>
    /// Slides a RectTransform in from off-screen and fades it in simultaneously.
    /// Call this on a panel that starts invisible (alpha 0, blocksRaycasts false).
    /// </summary>
    public static async UniTask SlideInAsync(RectTransform rect, CanvasGroup group,
                                             SlideDirection dir = SlideDirection.FromBottom,
                                             float duration = 0.4f)
    {
        Vector2 hiddenPos = OffscreenPosition(rect, dir);
        Vector2 shownPos  = rect.anchoredPosition;

        rect.anchoredPosition = hiddenPos;
        group.alpha           = 0f;
        group.interactable    = true;
        group.blocksRaycasts  = true;

#if DOTWEEN
        var seq = DOTween.Sequence();
        seq.Join(rect.DOAnchorPos(shownPos, duration).SetEase(Ease.OutCubic));
        seq.Join(group.DOFade(1f, duration * 0.6f));
        await seq.AsyncWaitForCompletion();
#else
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float s = EaseOutCubic(t);
            rect.anchoredPosition = Vector2.Lerp(hiddenPos, shownPos, s);
            group.alpha           = Mathf.Clamp01(t / 0.6f);
            await UniTask.NextFrame();
        }
        rect.anchoredPosition = shownPos;
        group.alpha           = 1f;
#endif
    }

    public static async UniTask SlideOutAsync(RectTransform rect, CanvasGroup group,
                                              SlideDirection dir = SlideDirection.FromBottom,
                                              float duration = 0.3f)
    {
        Vector2 shownPos  = rect.anchoredPosition;
        Vector2 hiddenPos = OffscreenPosition(rect, dir);

#if DOTWEEN
        var seq = DOTween.Sequence();
        seq.Join(rect.DOAnchorPos(hiddenPos, duration).SetEase(Ease.InCubic));
        seq.Join(group.DOFade(0f, duration));
        await seq.AsyncWaitForCompletion();
#else
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float s = EaseInCubic(t);
            rect.anchoredPosition = Vector2.Lerp(shownPos, hiddenPos, s);
            group.alpha           = 1f - t;
            await UniTask.NextFrame();
        }
#endif
        group.interactable   = false;
        group.blocksRaycasts = false;
    }

    // ─── Pop / Scale ─────────────────────────────────────────────────────────

    /// <summary>Bouncy pop-in effect — great for buttons and notification panels.</summary>
    public static async UniTask PopInAsync(Transform target, float duration = 0.4f)
    {
        target.localScale = Vector3.zero;
#if DOTWEEN
        await target.DOScale(Vector3.one, duration)
                    .SetEase(Ease.OutBack, 1.4f)
                    .AsyncWaitForCompletion();
#else
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float s = EaseOutBack(Mathf.Clamp01(t));
            target.localScale = Vector3.one * s;
            await UniTask.NextFrame();
        }
        target.localScale = Vector3.one;
#endif
    }

    /// <summary>Pulse a button briefly to draw attention.</summary>
    public static async UniTask PulseAsync(Transform target, float scale = 1.08f,
                                           float duration = 0.25f)
    {
#if DOTWEEN
        await target.DOPunchScale(Vector3.one * (scale - 1f), duration, 2, 0.4f)
                    .AsyncWaitForCompletion();
#else
        float half = duration * 0.5f;
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            target.localScale = Vector3.one * Mathf.Lerp(1f, scale, t / half);
            await UniTask.NextFrame();
        }
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            target.localScale = Vector3.one * Mathf.Lerp(scale, 1f, t / half);
            await UniTask.NextFrame();
        }
        target.localScale = Vector3.one;
#endif
    }

    // ─── Typewriter ──────────────────────────────────────────────────────────

    /// <summary>Reveals text one character at a time, like an old terminal.</summary>
    public static async UniTask TypewriterAsync(TMPro.TextMeshProUGUI label,
                                                string fullText,
                                                float charsPerSecond = 40f)
    {
        label.text = string.Empty;
        int total = fullText.Length;
        for (int i = 0; i <= total; i++)
        {
            label.text = fullText.Substring(0, i);
            await UniTask.Delay(TimeSpan.FromSeconds(1f / charsPerSecond));
        }
    }

    // ─── Screen Transitions ──────────────────────────────────────────────────

    /// <summary>
    /// Fades the screen to black, invokes an action (usually LoadScene), then
    /// fades back in.  Requires a full-screen black CanvasGroup covering the scene.
    /// </summary>
    public static async UniTask SceneTransitionAsync(CanvasGroup fullScreenBlack,
                                                     Action midAction,
                                                     float fadeDuration = 0.3f)
    {
        await FadeInAsync(fullScreenBlack, fadeDuration);
        midAction?.Invoke();
        await UniTask.Delay(TimeSpan.FromSeconds(0.1f)); // give Unity one frame to load
        await FadeOutAsync(fullScreenBlack, fadeDuration);
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    private static async UniTask LerpAlphaAsync(CanvasGroup group, float target, float duration)
    {
        float start = group.alpha;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t));
            await UniTask.NextFrame();
        }
        group.alpha = target;
    }

    private static Vector2 OffscreenPosition(RectTransform rect, SlideDirection dir)
    {
        float w = Screen.width;
        float h = Screen.height;
        Vector2 pos = rect.anchoredPosition;
        return dir switch
        {
            SlideDirection.FromBottom => new Vector2(pos.x, -h),
            SlideDirection.FromTop    => new Vector2(pos.x,  h),
            SlideDirection.FromLeft   => new Vector2(-w, pos.y),
            SlideDirection.FromRight  => new Vector2( w, pos.y),
            _                        => pos
        };
    }

    private static float EaseOutCubic(float t) { t = 1f - t; return 1f - t * t * t; }
    private static float EaseInCubic(float t)  => t * t * t;
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
