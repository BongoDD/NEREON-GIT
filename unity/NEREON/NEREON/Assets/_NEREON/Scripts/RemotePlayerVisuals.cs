using UnityEngine;

/// <summary>
/// Spawns and refreshes the visual representation of a remote player.
///
/// Lives on the Hub Player prefab.  For the LOCAL player this component is
/// dormant (HubPlayerNetwork drives the avatar via AvatarManager instead).
/// For REMOTE players, HubPlayerNetwork calls Refresh() whenever synced data
/// changes (avatar id, level, or username).
///
/// SCENE SETUP
/// ─────────────
/// 1. Add to Hub Player prefab.
/// 2. Wire _registry (AvatarRegistry asset).
/// 3. Wire _nameTagPrefab (FloatingNameTag prefab).
/// 4. Wire _effectsAnchor — a child Transform where the avatar model is placed.
/// </summary>
public class RemotePlayerVisuals : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private AvatarRegistry _registry;

    [Header("Scene")]
    [Tooltip("Child transform where the avatar model is parented.")]
    [SerializeField] private Transform _modelAnchor;

    [Tooltip("FloatingNameTag prefab — world-space billboard with username + level.")]
    [SerializeField] private FloatingNameTag _nameTagPrefab;

    // ── State ─────────────────────────────────────────────────────────────────

    private GameObject              _currentModel;
    private AvatarProgressionEffects _currentEffects;
    private FloatingNameTag          _nameTag;

    private byte   _lastAvatarId = 255; // sentinel — force first refresh
    private ushort _lastLevel;
    private string _lastUsername;

    // ── Public ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by HubPlayerNetwork whenever avatar id, level, or username changes.
    /// Avoids re-spawning the model if only level changed.
    /// </summary>
    public void Refresh(byte avatarId, ushort level, string username)
    {
        bool modelChanged = avatarId != _lastAvatarId;
        _lastAvatarId = avatarId;
        _lastLevel    = level;
        _lastUsername = username;

        if (modelChanged)
            SpawnModel(avatarId);

        if (_currentEffects != null)
            _currentEffects.Apply(level, 0); // XP not synced (level is enough for visuals)

        if (_nameTag != null)
            _nameTag.Initialise(username, level);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void SpawnModel(byte avatarId)
    {
        // Destroy previous
        if (_currentModel != null) Destroy(_currentModel);

        var prefab = _registry != null ? _registry.GetPrefab(avatarId) : null;
        if (prefab == null)
        {
            // Fallback: coloured placeholder capsule
            _currentModel = PlaceholderAvatarFactory.Create((PlaceholderAvatarFactory.Class)avatarId, _modelAnchor);
        }
        else
        {
            _currentModel = Instantiate(prefab, _modelAnchor);
        }

        _currentEffects = _currentModel.GetComponent<AvatarProgressionEffects>();
        if (_currentEffects == null)
            _currentEffects = _currentModel.AddComponent<AvatarProgressionEffects>();

        // Spawn or re-attach name tag
        if (_nameTagPrefab != null)
        {
            if (_nameTag != null) Destroy(_nameTag.gameObject);
            _nameTag = Instantiate(_nameTagPrefab, _currentModel.transform);
            _nameTag.Initialise(_lastUsername, _lastLevel);
        }
    }
}
