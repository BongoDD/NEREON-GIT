using UnityEngine;

/// <summary>
/// Runtime component added to every NEREON avatar prefab.
/// Holds the element type and applies the accent point-light glow.
/// Extend this later with particle VFX, emissive pulsing, etc.
/// </summary>
public class NereonAvatarSetup : MonoBehaviour
{
    public enum Element : byte { Fire = 0, Water = 1, Earth = 2, Air = 3 }

    [Header("Element")]
    public Element AvatarElement = Element.Fire;

    [Header("Colors")]
    public Color BodyColor   = Color.red;
    public Color AccentColor = Color.yellow;

    // Point light created at runtime (keeping prefabs light/portable)
    private Light _accentLight;

    private void Awake()
    {
        // Accent glow light
        var lightGO = new GameObject("AccentLight");
        lightGO.transform.SetParent(transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 1.1f, -0.3f);
        _accentLight           = lightGO.AddComponent<Light>();
        _accentLight.type      = LightType.Point;
        _accentLight.color     = AccentColor;
        _accentLight.intensity = 1.4f;
        _accentLight.range     = 3.5f;
        _accentLight.shadows   = LightShadows.None;
    }

    /// <summary>
    /// Called by AvatarProgressionEffects — boosts light intensity with level.
    /// Level 1-9 → normal, 10-19 → brighter, 20+ → pulsing (future).
    /// </summary>
    public void ApplyProgressionGlow(int level)
    {
        if (_accentLight == null) return;
        _accentLight.intensity = level >= 20 ? 3.0f : level >= 10 ? 2.0f : 1.4f;
        _accentLight.range     = level >= 10 ? 5f : 3.5f;
    }
}
