using UnityEngine;

/// <summary>
/// Generates a simple humanoid placeholder at runtime from Unity primitives.
/// No art assets required — works immediately out of the box.
///
/// DESIGN
/// ───────
/// Each "class" variant is the same capsule-body + sphere-head shape,
/// but with a distinct base color and a glowing accent light.
/// When the developer imports real character models, they register them in
/// AvatarRegistry.asset and the placeholder is never used again.
///
/// USAGE
/// ───────
///   var go = PlaceholderAvatarFactory.Create(PlaceholderAvatarFactory.Class.Warrior);
/// </summary>
public static class PlaceholderAvatarFactory
{
    // ── Class definitions ─────────────────────────────────────────────────────

    public enum Class : byte
    {
        Warrior = 0,
        Mage    = 1,
        Rogue   = 2,
        Paladin = 3,
    }

    public struct ClassInfo
    {
        public string DisplayName;
        public string Description;
        public Color  BodyColor;
        public Color  AccentColor;  // point-light glow colour
    }

    public static readonly ClassInfo[] Classes =
    {
        new ClassInfo
        {
            DisplayName  = "WARRIOR",
            Description  = "Fearless frontline fighter. High health, relentless aggression.",
            BodyColor    = new Color(0.75f, 0.15f, 0.10f), // deep red
            AccentColor  = new Color(1.00f, 0.25f, 0.10f),
        },
        new ClassInfo
        {
            DisplayName  = "MAGE",
            Description  = "Master of arcane arts. Low health, devastating skills.",
            BodyColor    = new Color(0.15f, 0.30f, 0.80f), // royal blue
            AccentColor  = new Color(0.40f, 0.60f, 1.00f),
        },
        new ClassInfo
        {
            DisplayName  = "ROGUE",
            Description  = "Shadow and speed. Precision strikes, high risk, high reward.",
            BodyColor    = new Color(0.10f, 0.55f, 0.20f), // forest green
            AccentColor  = new Color(0.30f, 1.00f, 0.45f),
        },
        new ClassInfo
        {
            DisplayName  = "PALADIN",
            Description  = "Blessed by the chain. Balanced stats, radiant aura.",
            BodyColor    = new Color(0.70f, 0.60f, 0.10f), // gold
            AccentColor  = new Color(1.00f, 0.90f, 0.30f),
        },
    };

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a placeholder humanoid GameObject for the given class.
    /// The returned object can be placed on the preview stage or in the world.
    /// </summary>
    public static GameObject Create(Class cls, Transform parent = null)
    {
        var info = Classes[(int)cls];

        var root = new GameObject($"Placeholder_{info.DisplayName}");
        if (parent != null) root.transform.SetParent(parent, false);

        // ── Body (capsule) ────────────────────────────────────────────────────
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        body.transform.localScale    = new Vector3(0.7f, 0.9f, 0.7f);
        ApplyColor(body, info.BodyColor);
        Object.Destroy(body.GetComponent<CapsuleCollider>()); // no physics in preview

        // ── Head (sphere) ─────────────────────────────────────────────────────
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0f, 2.15f, 0f);
        head.transform.localScale    = Vector3.one * 0.55f;
        ApplyColor(head, info.BodyColor);
        Object.Destroy(head.GetComponent<SphereCollider>());

        // ── Accent glow light ─────────────────────────────────────────────────
        var lightGO = new GameObject("AccentLight");
        lightGO.transform.SetParent(root.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 1.2f, -0.5f);
        var light = lightGO.AddComponent<Light>();
        light.type      = LightType.Point;
        light.color     = info.AccentColor;
        light.intensity = 1.8f;
        light.range     = 3f;

        return root;
    }

    /// <summary>
    /// Returns the ClassInfo for a given on-chain avatar_id byte.
    /// Falls back to Warrior if the id is out of range.
    /// </summary>
    public static ClassInfo GetInfo(byte avatarId)
    {
        int idx = avatarId < Classes.Length ? avatarId : 0;
        return Classes[idx];
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void ApplyColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;

        // Create a per-instance material so each avatar has its own colour
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ??
                               Shader.Find("Standard"));
        mat.color = color;
        renderer.material = mat;
    }
}
