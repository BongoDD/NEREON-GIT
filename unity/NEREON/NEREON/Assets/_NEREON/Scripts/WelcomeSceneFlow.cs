using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Master controller for WelcomeInitScene.
/// Manages a 3-step onboarding flow for first-time players:
///
///   Step 1 — AVATAR SELECT   : Browse and choose a character class.
///   Step 2 — NAME ENTRY      : Type a username (3–20 chars, trimmed).
///   Step 3 — CONFIRMING      : Sends initialize_user transaction on-chain.
///
/// This script owns all UI state transitions (using UIAnimations).
/// WelcomeInitController handles the actual blockchain transaction.
///
/// ─────────────────────────────────────────────────────────────────
/// SCENE HIERARCHY (build this in the Editor)
/// ─────────────────────────────────────────────────────────────────
///
/// WelcomeInitScene
/// ├── [Preview Stage]           ← empty GO at position (500, 0, 500), far from main cam
/// │   ├── PreviewCamera         ← Camera: Target Texture = AvatarPreviewRT.renderTexture
/// │   │                              Clear: Solid black | Culling: "AvatarPreview" layer
/// │   ├── PreviewLight          ← Directional Light, warm colour
/// │   └── PreviewAnchor         ← empty GO – the AvatarSelector._previewStage target
/// │
/// └── WelcomeCanvas             ← Canvas (Screen Space Overlay), CanvasScaler 1080p
///     │
///     ├── FadeOverlay           ← Image (black, full-screen) + CanvasGroup
///     │
///     ├── Panel_AvatarSelect    ← CanvasGroup, starts alpha=0
///     │   ├── Title             ← TMP "CHOOSE YOUR AVATAR"
///     │   ├── PreviewFrame      ← RawImage (AvatarPreviewRT) + outline border
///     │   ├── BtnLeft           ← Button "◄"
///     │   ├── BtnRight          ← Button "►"
///     │   ├── ClassNameLabel    ← TMP bold, large
///     │   ├── ClassDescLabel    ← TMP italic, small
///     │   ├── PageIndicator     ← TMP "1 / 4"
///     │   └── BtnNext           ← Button "NEXT  →"
///     │
///     ├── Panel_NameEntry       ← CanvasGroup, starts alpha=0
///     │   ├── Title             ← TMP "CHOOSE YOUR NAME"
///     │   ├── UsernameInput     ← TMP_InputField, limit 20 chars
///     │   ├── CharCount         ← TMP "0 / 20"
///     │   ├── BtnBack           ← Button "← BACK"
///     │   └── BtnConfirm        ← Button "ENTER NEREON"
///     │
///     └── Panel_Confirming      ← CanvasGroup, starts alpha=0
///         ├── LoadingSpinner    ← any rotating Image
///         └── StatusText        ← TMP "Saving to blockchain…"
///
/// After building the hierarchy, attach WelcomeSceneFlow to the FlowManager GO
/// and wire every [SerializeField] in the Inspector.
/// Also attach WelcomeInitController to the same GO (or a child).
/// ─────────────────────────────────────────────────────────────────
/// </summary>
public class WelcomeSceneFlow : MonoBehaviour
{
    // ── Sub-components ────────────────────────────────────────────────────────
    [Header("Transaction handler")]
    [SerializeField] private WelcomeInitController _txController;

    [Header("Avatar Selector sub-component")]
    [SerializeField] private AvatarSelector _avatarSelector;

    // ── Panels ────────────────────────────────────────────────────────────────
    [Header("Panels (CanvasGroup on each)")]
    [SerializeField] private CanvasGroup _panelAvatarSelect;
    [SerializeField] private CanvasGroup _panelNameEntry;
    [SerializeField] private CanvasGroup _panelConfirming;

    // ── Fade overlay ──────────────────────────────────────────────────────────
    [Header("Fade")]
    [SerializeField] private CanvasGroup _fadeOverlay;

    // ── Avatar Select panel ───────────────────────────────────────────────────
    [Header("Avatar Select — Buttons")]
    [SerializeField] private Button _btnNext;

    // ── Name Entry panel ──────────────────────────────────────────────────────
    [Header("Name Entry — Controls")]
    [SerializeField] private TMP_InputField _usernameInput;
    [SerializeField] private TextMeshProUGUI _charCountLabel;  // "0 / 20"
    [SerializeField] private Button _btnBack;
    [SerializeField] private Button _btnConfirm;
    [SerializeField] private TextMeshProUGUI _nameErrorLabel;  // "Name too short" etc.

    // ── Confirming panel ──────────────────────────────────────────────────────
    [Header("Confirming — Feedback")]
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private GameObject      _spinner;

    // ── Constants ─────────────────────────────────────────────────────────────
    private const int USERNAME_MIN = 3;
    private const int USERNAME_MAX = 20;

    // ── Unity ─────────────────────────────────────────────────────────────────
    private void Start()
    {
        // Wire avatar selector callback
        if (_avatarSelector != null)
            _avatarSelector.OnAvatarSelected += OnAvatarChanged;

        // Wire buttons
        _btnNext?.onClick.AddListener(GoToNameEntry);
        _btnBack?.onClick.AddListener(GoToAvatarSelect);
        _btnConfirm?.onClick.AddListener(OnConfirmClicked);

        // Wire username live validation
        _usernameInput?.onValueChanged.AddListener(OnUsernameChanged);

        // Kick off entry animation
        StartAsync().Forget();
    }

    private async UniTaskVoid StartAsync()
    {
        // Fade in from black → then show avatar select panel
        if (_fadeOverlay != null)
        {
            _fadeOverlay.alpha = 1f;
            await UIAnimations.FadeOutAsync(_fadeOverlay, 0.7f);
        }

        HideAllPanels();
        await UIAnimations.SlideInAsync(
            _panelAvatarSelect.GetComponent<RectTransform>(),
            _panelAvatarSelect,
            UIAnimations.SlideDirection.FromBottom);
    }

    // ── Step 1 → Step 2 ───────────────────────────────────────────────────────

    private void GoToNameEntry()
    {
        TransitionAsync(_panelAvatarSelect, _panelNameEntry).Forget();
    }

    // ── Step 2 → Step 1 ───────────────────────────────────────────────────────

    private void GoToAvatarSelect()
    {
        TransitionAsync(_panelNameEntry, _panelAvatarSelect,
                        fromDir: UIAnimations.SlideDirection.FromLeft,
                        toDir:   UIAnimations.SlideDirection.FromRight).Forget();
    }

    // ── Step 2 → Step 3 ───────────────────────────────────────────────────────

    private void OnConfirmClicked()
    {
        var username = _usernameInput != null
            ? _usernameInput.text.Trim()
            : "Adventurer";

        if (!ValidateUsername(username)) return;

        // Push values to the transaction controller
        if (_txController != null)
        {
            _txController.AvatarId = _avatarSelector != null
                ? _avatarSelector.SelectedAvatarId
                : (byte)0;
            _txController.Username = username;
        }

        TransitionAsync(_panelNameEntry, _panelConfirming).Forget();

        // Fire the on-chain transaction
        _txController?.CompleteSetup();
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────

    private void OnAvatarChanged(byte avatarId)
    {
        // Nothing extra needed — AvatarSelector already updates the preview
    }

    private void OnUsernameChanged(string value)
    {
        int len = value.Trim().Length;
        if (_charCountLabel) _charCountLabel.text = $"{len} / {USERNAME_MAX}";

        bool valid = len >= USERNAME_MIN && len <= USERNAME_MAX;
        if (_btnConfirm) _btnConfirm.interactable = valid;

        if (_nameErrorLabel)
        {
            _nameErrorLabel.text = len > 0 && len < USERNAME_MIN
                ? $"Minimum {USERNAME_MIN} characters"
                : string.Empty;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Called by WelcomeInitController to update the status text in the Confirming panel.</summary>
    public void SetConfirmStatus(string message, bool showSpinner)
    {
        if (_statusText) _statusText.text = message;
        if (_spinner)    _spinner.SetActive(showSpinner);
    }

    private bool ValidateUsername(string username)
    {
        if (username.Length < USERNAME_MIN)
        {
            if (_nameErrorLabel)
                _nameErrorLabel.text = $"Minimum {USERNAME_MIN} characters";
            return false;
        }
        if (username.Length > USERNAME_MAX)
        {
            if (_nameErrorLabel)
                _nameErrorLabel.text = $"Maximum {USERNAME_MAX} characters";
            return false;
        }
        return true;
    }

    private void HideAllPanels()
    {
        SetPanelVisible(_panelAvatarSelect, false);
        SetPanelVisible(_panelNameEntry,    false);
        SetPanelVisible(_panelConfirming,   false);
    }

    private static void SetPanelVisible(CanvasGroup cg, bool visible)
    {
        if (cg == null) return;
        cg.alpha          = visible ? 1f : 0f;
        cg.interactable   = visible;
        cg.blocksRaycasts = visible;
    }

    private async UniTask TransitionAsync(
        CanvasGroup from, CanvasGroup to,
        UIAnimations.SlideDirection fromDir = UIAnimations.SlideDirection.FromRight,
        UIAnimations.SlideDirection toDir   = UIAnimations.SlideDirection.FromRight)
    {
        // Slide out current panel
        if (from != null)
            await UIAnimations.SlideOutAsync(
                from.GetComponent<RectTransform>(), from, fromDir);

        // Slide in next panel
        if (to != null)
            await UIAnimations.SlideInAsync(
                to.GetComponent<RectTransform>(), to, toDir);
    }
}
