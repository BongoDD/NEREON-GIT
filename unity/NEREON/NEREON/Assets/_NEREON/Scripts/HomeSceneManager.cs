using System;
using Cysharp.Threading.Tasks;
using Solana.Unity.SDK;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages the NEREON hub-world (HomeScene).
///
/// Responsibilities
/// ─────────────────
/// • Fetches UserProfile + CharacterStats from Solana on scene load.
/// • Caches both for all other scripts to read (no repeated RPC calls).
/// • Triggers AvatarManager to spawn the correct avatar with progression effects.
/// • Starts the Blockade Labs AI skybox generation in the background.
/// • Handles XP refresh after returning from a mini-game.
///
/// SCENE SETUP
/// ──────────────────────────────────────────
/// 1. Create empty GameObject "HomeSceneManager" in HomeScene.
/// 2. Attach this script. Wire _hud, _skybox, _fadeOverlay in Inspector.
/// 3. AvatarManager.cs handles avatar spawning — wire it separately.
/// </summary>
public class HomeSceneManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static HomeSceneManager Instance { get; private set; }

    // ── Cached on-chain data ──────────────────────────────────────────────────
    public static CharacterStatsData? CachedStats   { get; private set; }
    public static UserProfileData?    CachedProfile { get; private set; } = null;
    public static string              CachedUsername { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("HUD")]
    [SerializeField] private PlayerHUD _hud;

    [Header("Avatar")]
    [SerializeField] private AvatarManager _avatarManager;

    [Header("Skybox")]
    [Tooltip("Leave empty to skip AI skybox generation on scene load.")]
    [SerializeField] private SkyboxController _skybox;

    [Tooltip("Prompt sent to Blockade Labs for the hub-world skybox.")]
    [SerializeField] private string _skyboxPrompt =
        "cute fantasy anime town, warm golden hour light, soft clouds, clear sky, Studio Ghibli style";

    [Header("Multiplayer")]
    [Tooltip("Wire the NereonNetworkManager component in the scene.")]
    [SerializeField] private NereonNetworkManager _networkManager;

    [Header("Fade")]
    [Tooltip("Full-screen black CanvasGroup for fade-in on scene load.")]
    [SerializeField] private CanvasGroup _fadeOverlay;

    // ── Unity ─────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start() => InitialiseAsync().Forget();

    // ── Initialisation ────────────────────────────────────────────────────────

    private async UniTaskVoid InitialiseAsync()
    {
        if (_fadeOverlay != null)
        {
            _fadeOverlay.alpha = 1f;
            await UIAnimations.FadeOutAsync(_fadeOverlay, 0.6f);
        }

        if (Web3.Account == null)
        {
            Debug.LogWarning("[HomeSceneManager] No wallet — back to Landing.");
            SceneManager.LoadScene("LandingScene");
            return;
        }

        await LoadOnChainDataAsync();

        // Fire-and-forget: generate AI skybox in the background while player plays
        if (_skybox != null && !string.IsNullOrWhiteSpace(_skyboxPrompt))
            _skybox.GenerateAndApplyAsync(_skyboxPrompt).Forget();
    }

    /// <summary>
    /// Fetches both on-chain accounts, updates HUD and spawns/refreshes the avatar.
    /// Call again after a mini-game to reflect new XP / level-up effects.
    /// </summary>
    public async UniTask LoadOnChainDataAsync()
    {
        try
        {
            var wallet = Web3.Account.PublicKey;

            var statsTask   = NereonClient.FetchCharacterStatsAsync(wallet);
            var profileTask = NereonClient.FetchUserProfileAsync(wallet);
            await UniTask.WhenAll(statsTask, profileTask);

            CachedStats   = statsTask.GetAwaiter().GetResult();
            CachedProfile = profileTask.GetAwaiter().GetResult();

            if (CachedStats == null)
            {
                Debug.LogWarning("[HomeSceneManager] CharacterStats not found — redirecting to WelcomeInit.");
                SceneManager.LoadScene("WelcomeInitScene");
                return;
            }

            CachedUsername = CachedProfile.HasValue
                ? NereonClient.DecodeUsername(CachedProfile.Value.Username)
                : "Adventurer";

            // Update HUD
            _hud?.Refresh(CachedUsername, CachedStats.Value.Level, CachedStats.Value.Xp);

            // Spawn or refresh the player's avatar with on-chain cosmetics
            if (_avatarManager != null && CachedProfile.HasValue)
                await _avatarManager.LoadAvatarAsync(CachedProfile.Value, CachedStats.Value);

            // Connect to the multiplayer hub (after identity is known)
            _networkManager?.ConnectAsync(wallet.ToString());
        }
        catch (Exception e)
        {
            Debug.LogError($"[HomeSceneManager] Failed to load on-chain data: {e.Message}");
        }
    }
}
