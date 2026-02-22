using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Attach to each building GameObject in the hub town.
///
/// HOW IT WORKS
/// ─────────────
/// When the player walks into the trigger collider:
///   1. Check CachedStats.Level >= MinLevelRequired (WorldConfig data, no RPC needed).
///   2. If eligible:   show "Press E to Enter" prompt.
///      If ineligible: show "Requires LVL X" padlock prompt.
///   3. On E-press:    fade out, set MiniGameContext.CurrentGameId, load mini-game scene.
///
/// SETUP IN EDITOR (per building)
/// ───────────────────────────────
/// 1. Add a Collider to the building (or an empty child) — set "Is Trigger" ON.
/// 2. Attach this script.
/// 3. Set GameId (must match WorldConfig + on-chain game_id byte).
/// 4. Drag in the WorldConfig asset.
/// 5. Wire _entryPromptPanel, _promptLabel, _lockedLabel, _fadeOverlay.
///
/// The building does NOT need a separate MiniGamePortal — this replaces it.
/// </summary>
public class BuildingInteraction : MonoBehaviour
{
    // ── Identity ──────────────────────────────────────────────────────────────
    [Header("Building Identity")]
    [Tooltip("Must match the game_id byte in WorldConfig and the Anchor program.")]
    [Range(0, 255)]
    public byte GameId = 0;

    [Tooltip("The central WorldConfig asset (Create → NEREON → World Config).")]
    [SerializeField] private WorldConfig _worldConfig;

    // ── UI ────────────────────────────────────────────────────────────────────
    [Header("Entry Prompt UI")]
    [Tooltip("Parent panel that contains both the eligible and locked prompts.")]
    [SerializeField] private GameObject _entryPromptPanel;

    [Tooltip("TMP label shown when eligible: 'Press E — The Coin Flip\\nLevel 1+'")]
    [SerializeField] private TextMeshProUGUI _promptLabel;

    [Tooltip("TMP label shown when locked: '🔒 Requires LVL X'")]
    [SerializeField] private TextMeshProUGUI _lockedLabel;

    [Tooltip("Optional building sign label showing the game name (world space).")]
    [SerializeField] private TextMeshProUGUI _buildingSignLabel;

    // ── Scene Transition ──────────────────────────────────────────────────────
    [Header("Scene Transition")]
    [Tooltip("Full-screen CanvasGroup for fade-to-black transition. Can be shared.")]
    [SerializeField] private CanvasGroup _fadeOverlay;

    // ── Private State ─────────────────────────────────────────────────────────
    private bool _playerInRange;
    private bool _isEligible;
    private BuildingDefinition _def;
    private bool _hasDefinition;

    // ── Unity ─────────────────────────────────────────────────────────────────
    private void Start()
    {
        // Read definition from WorldConfig
        _hasDefinition = _worldConfig != null &&
                         _worldConfig.TryGetBuilding(GameId, out _def);

        // Set the world-space building sign
        if (_hasDefinition && _buildingSignLabel != null)
            _buildingSignLabel.text = _def.DisplayName;

        // Start with prompt hidden
        if (_entryPromptPanel) _entryPromptPanel.SetActive(false);
    }

    private void Update()
    {
        if (!_playerInRange || !_isEligible) return;
        if (Input.GetKeyDown(KeyCode.E)) EnterBuildingAsync().Forget();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = true;
        ShowPrompt();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = false;
        if (_entryPromptPanel) _entryPromptPanel.SetActive(false);
    }

    // ── Prompt Display ────────────────────────────────────────────────────────

    private void ShowPrompt()
    {
        if (!_hasDefinition) return;
        if (_entryPromptPanel) _entryPromptPanel.SetActive(true);

        ushort playerLevel = HomeSceneManager.CachedStats?.Level ?? 0;
        _isEligible = playerLevel >= _def.MinLevelRequired;

        if (_isEligible)
        {
            if (_promptLabel) _promptLabel.text =
                $"<b>Press E</b> to enter\n<size=70%>{_def.DisplayName}</size>";
            if (_lockedLabel) _lockedLabel.gameObject.SetActive(false);
            if (_promptLabel) _promptLabel.gameObject.SetActive(true);
        }
        else
        {
            if (_lockedLabel) _lockedLabel.text =
                $"🔒 Requires LVL {_def.MinLevelRequired}\n<size=70%>{_def.DisplayName}</size>";
            if (_promptLabel) _promptLabel.gameObject.SetActive(false);
            if (_lockedLabel) _lockedLabel.gameObject.SetActive(true);
        }
    }

    // ── Entry ─────────────────────────────────────────────────────────────────

    private async UniTaskVoid EnterBuildingAsync()
    {
        if (!_hasDefinition) return;

        _playerInRange = false; // prevent double-trigger
        if (_entryPromptPanel) _entryPromptPanel.SetActive(false);

        // Fade out
        if (_fadeOverlay != null)
            await UIAnimations.FadeInAsync(_fadeOverlay, 0.3f);

        // Pass context to mini-game scene (no PlayerPrefs — static context bag)
        MiniGameContext.CurrentGameId = GameId;
        MiniGameContext.CurrentGameDef = _def;

        SceneManager.LoadScene(_def.MiniGameScene);
    }
}
