using UnityEngine;

/// <summary>
/// Attach to every avatar prefab.
/// Applies level-based visual effects — aura particles, outline glow, trail FX —
/// all driven purely by the player's on-chain CharacterStats.level.
///
/// TIER TABLE (matches on-chain XP formula: level * 100 XP to level up)
/// ──────────────────────────────────────────────────────────────────────
///  Tier 0   Levels  1–5    No effects     (newcomer)
///  Tier 1   Levels  6–10   Soft blue aura (regular player)
///  Tier 2   Levels 11–20   Purple aura + subtle trail (veteran)
///  Tier 3   Levels 21–35   Gold aura + bright trail (elite)
///  Tier 4   Levels 36+     Rainbow aura + full trail + ground rune (legend)
///
/// ACHIEVEMENTS / ITEMS (future expansion)
/// ─────────────────────────────────────────
/// Wire the _achievementSlot GameObjects in the Inspector.
/// Enable them from AvatarManager when the player has the relevant on-chain badge.
///
/// TOONY COLORS PRO 2 INTEGRATION
/// ─────────────────────────────────────────
/// After importing TCP2, change the avatar material's shader to one of:
///   "Toony Colors Pro 2/Mobile/..." or "Toony Colors Pro 2/Outline..."
/// Then wire _avatarRenderer here. The outline color and width are set per tier.
///
/// SETUP IN EDITOR
/// ─────────────────────────────────────────
/// 1. Add this component to your avatar prefab.
/// 2. Create 4 ParticleSystem GameObjects inside the prefab (aura, trail, rune, burst).
///    Drag them into the tier slots below.
/// 3. (Optional) Wire _avatarRenderer and _outlineMatPropertyBlock for TCP2 outline.
/// </summary>
[DisallowMultipleComponent]
public class AvatarProgressionEffects : MonoBehaviour
{
    // ── Particle Systems ──────────────────────────────────────────────────────
    [Header("Aura Particles (one per tier — leave null until you create them)")]
    [Tooltip("Tier 1: soft blue mist particles around the body.")]
    [SerializeField] private ParticleSystem _tier1Aura;

    [Tooltip("Tier 2: purple aura + small orbiting sparks.")]
    [SerializeField] private ParticleSystem _tier2Aura;

    [Tooltip("Tier 3: gold aura + bright sparkle burst on level-up.")]
    [SerializeField] private ParticleSystem _tier3Aura;

    [Tooltip("Tier 4: rainbow aura (full legend effect).")]
    [SerializeField] private ParticleSystem _tier4Aura;

    [Header("Trail")]
    [Tooltip("Trail renderer that activates at Tier 2+.")]
    [SerializeField] private TrailRenderer _trail;

    [Header("Ground Rune")]
    [Tooltip("Ground-projected particle (rune / circle glow) — activates at Tier 4.")]
    [SerializeField] private ParticleSystem _groundRune;

    [Header("Outline (Toony Colors Pro 2)")]
    [Tooltip("Main SkinnedMeshRenderer of the avatar — used to drive outline color.")]
    [SerializeField] private SkinnedMeshRenderer _avatarRenderer;

    // ── Outline colors per tier (set to match your art direction) ─────────────
    [Header("Outline Colors per Tier (TCP2)")]
    [SerializeField] private Color _tier0OutlineColor = Color.black;
    [SerializeField] private Color _tier1OutlineColor = new Color(0.4f, 0.7f, 1.0f);  // blue
    [SerializeField] private Color _tier2OutlineColor = new Color(0.7f, 0.3f, 1.0f);  // purple
    [SerializeField] private Color _tier3OutlineColor = new Color(1.0f, 0.8f, 0.0f);  // gold
    [SerializeField] private Color _tier4OutlineColor = new Color(1.0f, 0.5f, 1.0f);  // rainbow base (pink)

    [Header("Achievement Slots (enable when player earns them on-chain)")]
    [Tooltip("E.g. a crown mesh for reaching Tier 4, a cape for X games played, etc.")]
    [SerializeField] private GameObject[] _achievementSlots = System.Array.Empty<GameObject>();

    // ── Public ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by AvatarManager after on-chain data is fetched.
    /// Enables exactly the right effects for this player's level.
    /// </summary>
    public void Apply(ushort level, uint xp)
    {
        int tier = LevelToTier(level);
        ApplyAura(tier);
        ApplyTrail(tier);
        ApplyGroundRune(tier);
        ApplyOutline(tier);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static int LevelToTier(ushort level) => level switch
    {
        >= 36 => 4,
        >= 21 => 3,
        >= 11 => 2,
        >= 6  => 1,
        _     => 0
    };

    private void ApplyAura(int tier)
    {
        SetParticle(_tier1Aura, tier == 1);
        SetParticle(_tier2Aura, tier == 2);
        SetParticle(_tier3Aura, tier == 3);
        SetParticle(_tier4Aura, tier >= 4);
    }

    private void ApplyTrail(int tier)
    {
        if (_trail == null) return;
        _trail.enabled = tier >= 2;
        if (tier >= 2)
        {
            // Trail color shifts with tier
            _trail.startColor = tier >= 4 ? new Color(1f, 0.5f, 1f) :
                                tier == 3 ? new Color(1f, 0.8f, 0f) :
                                            new Color(0.7f, 0.3f, 1f);
            _trail.endColor = new Color(_trail.startColor.r, _trail.startColor.g,
                                        _trail.startColor.b, 0f);
        }
    }

    private void ApplyGroundRune(int tier)
    {
        SetParticle(_groundRune, tier >= 4);
    }

    private void ApplyOutline(int tier)
    {
        if (_avatarRenderer == null) return;
        // TCP2 exposes "_OutlineColor" on materials using its outline shader.
        // This is a MaterialPropertyBlock write — no material instance needed.
        var block = new MaterialPropertyBlock();
        _avatarRenderer.GetPropertyBlock(block);
        block.SetColor("_OutlineColor", tier switch
        {
            1 => _tier1OutlineColor,
            2 => _tier2OutlineColor,
            3 => _tier3OutlineColor,
            4 => _tier4OutlineColor,
            _ => _tier0OutlineColor
        });
        _avatarRenderer.SetPropertyBlock(block);
    }

    private static void SetParticle(ParticleSystem ps, bool active)
    {
        if (ps == null) return;
        if (active && !ps.isPlaying)  ps.Play();
        if (!active && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
