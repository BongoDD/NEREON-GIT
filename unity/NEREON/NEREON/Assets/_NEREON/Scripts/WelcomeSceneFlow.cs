using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Master controller for WelcomeInitScene — 4-step onboarding flow:
///
///   Step 0 — INTRO          : Who you are, what elements mean, how the game works.
///   Step 1 — AVATAR SELECT  : Browse the 4 elemental avatars (carousel).
///   Step 2 — NAME ENTRY     : Choose a username (3–20 chars).
///   Step 3 — CONFIRMING     : Sends initialize_user transaction on-chain.
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
    [SerializeField] private CanvasGroup _panelIntro;
    [SerializeField] private CanvasGroup _panelAvatarSelect;
    [SerializeField] private CanvasGroup _panelNameEntry;
    [SerializeField] private CanvasGroup _panelConfirming;

    // ── Fade overlay ──────────────────────────────────────────────────────────

    [Header("Fade")]
    [SerializeField] private CanvasGroup _fadeOverlay;

    // ── Intro panel ───────────────────────────────────────────────────────────

    [Header("Intro — Buttons")]
    [SerializeField] private Button _btnBegin;

    // ── Avatar Select panel ───────────────────────────────────────────────────

    [Header("Avatar Select — Buttons")]
    [SerializeField] private Button _btnNext;

    // ── Name Entry panel ──────────────────────────────────────────────────────

    [Header("Name Entry — Controls")]
    [SerializeField] private TMP_InputField  _usernameInput;
    [SerializeField] private TextMeshProUGUI _charCountLabel;
    [SerializeField] private Button          _btnBack;
    [SerializeField] private Button          _btnConfirm;
    [SerializeField] private TextMeshProUGUI _nameErrorLabel;

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
        if (_avatarSelector != null)
            _avatarSelector.OnAvatarSelected += OnAvatarChanged;

        _btnBegin?.onClick.AddListener(GoToAvatarSelect);
        _btnNext?.onClick.AddListener(GoToNameEntry);
        _btnBack?.onClick.AddListener(GoToAvatarSelect);
        _btnConfirm?.onClick.AddListener(OnConfirmClicked);

        _usernameInput?.onValueChanged.AddListener(OnUsernameChanged);

        StartAsync().Forget();
    }

    private async UniTaskVoid StartAsync()
    {
        if (_fadeOverlay != null)
        {
            _fadeOverlay.alpha = 1f;
            await UIAnimations.FadeOutAsync(_fadeOverlay, 0.7f);
        }

        HideAllPanels();

        // Start on the intro panel
        await UIAnimations.SlideInAsync(
            _panelIntro.GetComponent<RectTransform>(),
            _panelIntro,
            UIAnimations.SlideDirection.FromBottom);
    }

    // ── Step 0 → Step 1 ───────────────────────────────────────────────────────

    private void GoToAvatarSelect()
    {
        if (_panelIntro != null && _panelIntro.alpha > 0f)
            TransitionAsync(_panelIntro, _panelAvatarSelect).Forget();
        else
            TransitionAsync(_panelNameEntry, _panelAvatarSelect,
                fromDir: UIAnimations.SlideDirection.FromLeft,
                toDir:   UIAnimations.SlideDirection.FromRight).Forget();
    }

    // ── Step 1 → Step 2 ───────────────────────────────────────────────────────

    private void GoToNameEntry()
    {
        TransitionAsync(_panelAvatarSelect, _panelNameEntry).Forget();
    }

    // ── Step 2 → Step 3 ───────────────────────────────────────────────────────

    private void OnConfirmClicked()
    {
        var username = _usernameInput != null ? _usernameInput.text.Trim() : "Adventurer";
        if (!ValidateUsername(username)) return;

        byte avatarId = _avatarSelector != null ? _avatarSelector.SelectedAvatarId : (byte)0;

        if (_txController != null)
        {
            _txController.AvatarId = avatarId;
            _txController.Username = username;
        }

        // Save locally so HomeScene can load avatar even if chain tx hasn't confirmed
        PlayerPrefs.SetInt("NEREON_AvatarId", avatarId);
        PlayerPrefs.SetString("NEREON_Username", username);
        PlayerPrefs.Save();

        TransitionAsync(_panelNameEntry, _panelConfirming).Forget();
        _txController?.CompleteSetup();
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────

    private void OnAvatarChanged(byte avatarId) { }

    private void OnUsernameChanged(string value)
    {
        int len = value.Trim().Length;
        if (_charCountLabel) _charCountLabel.text = $"{len} / {USERNAME_MAX}";

        bool valid = len >= USERNAME_MIN && len <= USERNAME_MAX;
        if (_btnConfirm) _btnConfirm.interactable = valid;

        if (_nameErrorLabel)
            _nameErrorLabel.text = len > 0 && len < USERNAME_MIN
                ? $"Minimum {USERNAME_MIN} characters" : string.Empty;
    }

    // ── Public ────────────────────────────────────────────────────────────────

    public void SetConfirmStatus(string message, bool showSpinner)
    {
        if (_statusText) _statusText.text = message;
        if (_spinner)    _spinner.SetActive(showSpinner);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private bool ValidateUsername(string username)
    {
        if (username.Length < USERNAME_MIN)
        {
            if (_nameErrorLabel) _nameErrorLabel.text = $"Minimum {USERNAME_MIN} characters";
            return false;
        }
        if (username.Length > USERNAME_MAX)
        {
            if (_nameErrorLabel) _nameErrorLabel.text = $"Maximum {USERNAME_MAX} characters";
            return false;
        }
        return true;
    }

    private void HideAllPanels()
    {
        SetPanelVisible(_panelIntro,         false);
        SetPanelVisible(_panelAvatarSelect,  false);
        SetPanelVisible(_panelNameEntry,     false);
        SetPanelVisible(_panelConfirming,    false);
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
        if (from != null)
            await UIAnimations.SlideOutAsync(from.GetComponent<RectTransform>(), from, fromDir);
        if (to != null)
            await UIAnimations.SlideInAsync(to.GetComponent<RectTransform>(), to, toDir);
    }
}
