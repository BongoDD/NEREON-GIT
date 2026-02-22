using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Displays the player's username, level, and XP on the in-game HUD.
///
/// SCENE SETUP
/// ──────────────────────────────────────────────
/// Create a Canvas (Screen Space – Overlay) in HomeScene, add:
///   • TMP label  →  wire to _usernameText
///   • TMP label  →  wire to _levelText  (e.g. "LVL 7")
///   • TMP label  →  wire to _xpText     (e.g. "2350 / 3000 XP")
///   • Slider     →  wire to _xpBar
///
/// Call Refresh() from HomeSceneManager after loading on-chain data.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [Header("Labels")]
    [SerializeField] private TextMeshProUGUI _usernameText;
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _xpText;

    [Header("XP Bar")]
    [SerializeField] private Slider _xpBar;

    // XP formula must match the on-chain program in lib.rs:
    //   xp_needed = level * XP_LEVEL_BASE (100)
    private const int XP_LEVEL_BASE = 100;

    /// <summary>
    /// Updates all HUD elements with the latest on-chain values.
    /// Safe to call from any thread via UniTask .
    /// </summary>
    public void Refresh(string username, ushort level, uint xp)
    {
        if (_usernameText) _usernameText.text = username;
        if (_levelText)    _levelText.text    = $"LVL {level}";

        uint xpNeeded = (uint)(level * XP_LEVEL_BASE);
        if (_xpText) _xpText.text = $"{xp} / {xpNeeded} XP";
        if (_xpBar)
        {
            _xpBar.minValue = 0f;
            _xpBar.maxValue = xpNeeded;
            _xpBar.value    = xp;
        }
    }
}
