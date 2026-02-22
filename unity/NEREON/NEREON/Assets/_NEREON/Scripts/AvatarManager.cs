using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Spawns and manages the local player's avatar in HomeScene.
///
/// HOW IT WORKS
/// ─────────────
/// 1. Reads avatar_id from the on-chain UserProfile.
/// 2. Looks up the matching prefab in AvatarRegistry.
/// 3. Instantiates it at the SpawnPoint.
/// 4. Applies progression cosmetics via AvatarProgressionEffects.
/// 5. Attaches a FloatingNameTag above the head.
///
/// Called by HomeSceneManager.LoadOnChainDataAsync() — once on load,
/// and again after returning from a mini-game to reflect XP/level-ups.
///
/// SCENE SETUP
/// ─────────────
/// 1. Add AvatarManager component to the HomeSceneManager GameObject (or a
///    dedicated "Managers" GO in HomeScene).
/// 2. Wire _registry (AvatarRegistry asset) and _spawnPoint in the Inspector.
/// 3. Wire HomeSceneManager._avatarManager to this component.
/// </summary>
public class AvatarManager : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private AvatarRegistry _registry;

    [Header("Scene")]
    [Tooltip("Where the player avatar spawns when HomeScene loads. " +
             "Place this at the Central Plaza centre.")]
    [SerializeField] private Transform _spawnPoint;

    [Tooltip("Prefab for the floating name-tag above the player's head.")]
    [SerializeField] private FloatingNameTag _nameTagPrefab;

    // ── State ─────────────────────────────────────────────────────────────────
    private GameObject          _currentAvatar;
    private AvatarProgressionEffects _currentEffects;

    // ── Public ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns (or refreshes) the player avatar based on on-chain data.
    /// Safe to call multiple times — destroys the old avatar first.
    /// </summary>
    public async UniTask LoadAvatarAsync(UserProfileData profile, CharacterStatsData stats)
    {
        await UniTask.SwitchToMainThread();

        // Tear down any existing avatar (e.g. re-entry after mini-game)
        if (_currentAvatar != null) Destroy(_currentAvatar);

        var prefab = _registry != null ? _registry.GetPrefab(profile.AvatarId) : null;
        if (prefab == null)
        {
            Debug.LogError("[AvatarManager] No prefab found — cannot spawn avatar.");
            return;
        }

        // Spawn at spawn point
        var spawnPos = _spawnPoint != null ? _spawnPoint.position : Vector3.zero;
        var spawnRot = _spawnPoint != null ? _spawnPoint.rotation : Quaternion.identity;
        _currentAvatar = Instantiate(prefab, spawnPos, spawnRot);
        _currentAvatar.name = "PlayerAvatar";
        _currentAvatar.tag  = "Player";

        // Apply progression-based visual effects
        _currentEffects = _currentAvatar.GetComponent<AvatarProgressionEffects>();
        if (_currentEffects == null)
            _currentEffects = _currentAvatar.AddComponent<AvatarProgressionEffects>();
        _currentEffects.Apply(stats.Level, stats.Xp);

        // Attach floating name tag
        if (_nameTagPrefab != null)
        {
            var tag = Instantiate(_nameTagPrefab, _currentAvatar.transform);
            tag.Initialise(HomeSceneManager.CachedUsername, stats.Level);
        }

        Debug.Log($"[AvatarManager] Avatar spawned: id={profile.AvatarId}, level={stats.Level}");
    }

    /// <summary>Updates effects only (no re-spawn) — call after XP gain in the hub.</summary>
    public void RefreshEffects(CharacterStatsData stats)
    {
        if (_currentEffects == null) return;
        _currentEffects.Apply(stats.Level, stats.Xp);
    }
}
