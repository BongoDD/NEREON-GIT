using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Displays speech bubbles above a player's head when they send a chat message.
///
/// Bubbles stack vertically (newest at bottom), fade out after a few seconds,
/// and are shown on ALL clients via ClientRpc in HubPlayerNetwork.
///
/// SCENE SETUP
/// ─────────────
/// 1. Add to Hub Player prefab.
/// 2. Wire _bubbleContainer — a child Transform positioned above the player's head
///    (e.g. at local Y = 2.4, same anchor as FloatingNameTag but slightly higher).
/// 3. Wire _bubblePrefab — a World Space Canvas prefab containing:
///      - Background Image (white rounded rect, 10px padding)
///      - TMP_Text for the message
///      - CanvasGroup (for alpha fade)
/// </summary>
public class BubbleChat : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Parent transform above the player's head where bubbles stack.")]
    [SerializeField] private Transform _bubbleContainer;

    [Tooltip("World-space canvas prefab for a single chat bubble.")]
    [SerializeField] private GameObject _bubblePrefab;

    [Header("Settings")]
    [Tooltip("How long a bubble stays fully visible before fading.")]
    [SerializeField] private float _visibleDuration  = 4f;

    [Tooltip("How long the fade-out takes.")]
    [SerializeField] private float _fadeDuration     = 1f;

    [Tooltip("Max simultaneous bubbles visible above a player.")]
    [SerializeField] private int   _maxBubbles       = 3;

    [Tooltip("Vertical spacing between stacked bubbles (world units).")]
    [SerializeField] private float _stackSpacing     = 0.35f;

    // ── State ─────────────────────────────────────────────────────────────────

    // Active bubbles (newest last, oldest first)
    private readonly List<GameObject> _activeBubbles = new();

    // ── Public ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawn a new speech bubble with the given text.
    /// Called on ALL clients by HubPlayerNetwork.ShowChat_ClientRpc.
    /// </summary>
    public void ShowMessage(string text)
    {
        if (_bubblePrefab == null || _bubbleContainer == null)
        {
            Debug.LogWarning("[BubbleChat] Bubble prefab or container not wired.");
            return;
        }

        // Evict oldest bubble if at max
        if (_activeBubbles.Count >= _maxBubbles)
        {
            var oldest = _activeBubbles[0];
            _activeBubbles.RemoveAt(0);
            Destroy(oldest);
            RepositionBubbles();
        }

        // Spawn new bubble at height = container + (count * spacing)
        var bubble = Instantiate(_bubblePrefab, _bubbleContainer);
        bubble.transform.localPosition = new Vector3(0f, _activeBubbles.Count * _stackSpacing, 0f);
        bubble.transform.localRotation = Quaternion.identity;

        // Set text
        var tmp = bubble.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.text = text;

        _activeBubbles.Add(bubble);

        // Make it face the camera
        StartCoroutine(FaceCamera(bubble.transform));
        // Schedule fade and removal
        StartCoroutine(FadeAndRemove(bubble));
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void RepositionBubbles()
    {
        for (int i = 0; i < _activeBubbles.Count; i++)
        {
            if (_activeBubbles[i] == null) continue;
            _activeBubbles[i].transform.localPosition = new Vector3(0f, i * _stackSpacing, 0f);
        }
    }

    private IEnumerator FadeAndRemove(GameObject bubble)
    {
        var cg = bubble.GetComponent<CanvasGroup>();
        if (cg == null) cg = bubble.AddComponent<CanvasGroup>();

        // Visible phase
        yield return new WaitForSeconds(_visibleDuration);

        // Fade out
        float elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (cg != null) cg.alpha = 1f - (elapsed / _fadeDuration);
            yield return null;
        }

        _activeBubbles.Remove(bubble);
        Destroy(bubble);
        RepositionBubbles();
    }

    private IEnumerator FaceCamera(Transform bubbleTransform)
    {
        // Keep facing camera every frame until destroyed
        while (bubbleTransform != null)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                bubbleTransform.rotation = Quaternion.LookRotation(
                    bubbleTransform.position - cam.transform.position);
            }
            yield return null;
        }
    }
}
