using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach to any trigger-collider GameObject in the hub town to create a
/// mini-game portal.  When the player walks into the zone, a prompt appears.
/// When the player presses [E] / South gamepad button, the mini-game loads.
///
/// SETUP (per portal in the Editor)
/// ─────────────────────────────────
/// 1. Create a GameObject with a Collider (Box / Sphere), set "Is Trigger" ON.
/// 2. Attach this script.
/// 3. Set GameId (0–255) — must match the on-chain game_id byte in the Anchor program.
/// 4. Set MiniGameSceneName to the exact scene name added in Build Settings.
/// 5. Wire _promptUI to a Canvas/panel that shows "Press E to enter".
///
/// RETURNING FROM A MINI-GAME
/// ───────────────────────────
/// After the mini-game is done, load "HomeScene".
/// HomeSceneManager.Instance.LoadOnChainDataAsync() is called automatically on Start,
/// so the HUD XP will update to reflect the newly posted score.
/// </summary>
public class MiniGamePortal : MonoBehaviour
{
    [Header("Portal Identity")]
    [Tooltip("0–255 — must match the game_id byte in the on-chain Anchor program.")]
    [Range(0, 255)]
    public byte GameId = 0;

    [Tooltip("Exact scene name (as in Build Settings) for this mini-game.")]
    public string MiniGameSceneName = "MiniGame_Placeholder";

    [Header("UI")]
    [Tooltip("Panel / world-space UI that appears when the player is in range.")]
    [SerializeField] private GameObject _promptUI;

    [Tooltip("Fade overlay CanvasGroup for smooth scene transition.")]
    [SerializeField] private CanvasGroup _fadeOverlay;

    // ── State ─────────────────────────────────────────────────────────────────
    private bool _playerInRange;

    // ── Unity ─────────────────────────────────────────────────────────────────
    private void Update()
    {
        if (!_playerInRange) return;
        // New Input System: check for "Interact" action — or fall back to KeyCode.E
        if (Input.GetKeyDown(KeyCode.E)) EnterPortalAsync().Forget();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = true;
        if (_promptUI) _promptUI.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = false;
        if (_promptUI) _promptUI.SetActive(false);
    }

    // ── Portal enter ─────────────────────────────────────────────────────────

    private async UniTaskVoid EnterPortalAsync()
    {
        _playerInRange = false; // prevent double-trigger
        if (_promptUI) _promptUI.SetActive(false);

        // Fade to black
        if (_fadeOverlay != null)
            await UIAnimations.FadeInAsync(_fadeOverlay, 0.3f);

        // Store the game ID so the mini-game scene can read it without coupling
        MiniGameContext.CurrentGameId = GameId;

        SceneManager.LoadScene(MiniGameSceneName);
    }
}

/// <summary>
/// Tiny static context bag — avoids coupling scenes through PlayerPrefs for
/// the transient "which mini-game am I playing?" state.
/// </summary>
public static class MiniGameContext
{
    /// <summary>Set by MiniGamePortal / BuildingInteraction before loading the mini-game scene.</summary>
    public static byte CurrentGameId { get; set; }

    /// <summary>Full building definition — gives the mini-game scene access to display name, description, etc.</summary>
    public static BuildingDefinition CurrentGameDef { get; set; }
}
