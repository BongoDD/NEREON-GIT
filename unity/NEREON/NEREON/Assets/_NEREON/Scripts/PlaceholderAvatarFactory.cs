using UnityEngine;

/// <summary>
/// Generates a simple humanoid placeholder at runtime from Unity primitives.
/// No art assets required — works immediately out of the box.
///
/// ELEMENTS: Fire · Water · Earth · Air
/// Your element is cosmetic — it represents your identity in NEREON.
/// Mini-games are purely skill-based; element does not affect gameplay.
///
/// When the developer imports real character models, register them in
/// AvatarRegistry.asset and this placeholder is never used again.
/// </summary>
public static class PlaceholderAvatarFactory
{
    // ── Element definitions ───────────────────────────────────────────────────

    public enum Class : byte
    {
        Fire  = 0,
        Water = 1,
        Earth = 2,
        Air   = 3,
    }

    public struct ClassInfo
    {
        public string DisplayName;
        public string Description;
        public Color  BodyColor;
        public Color  AccentColor;
    }

    public static readonly ClassInfo[] Classes =
    {
        new ClassInfo
        {
            DisplayName  = "FIRE",
            Description  = "Fierce and relentless. The Fire avatar burns with passion and raw power.",
            BodyColor    = new Color(0.80f, 0.18f, 0.05f), // deep ember red
            AccentColor  = new Color(1.00f, 0.50f, 0.05f), // bright orange glow
        },
        new ClassInfo
        {
            DisplayName  = "WATER",
            Description  = "Fluid and adaptive. The Water avatar flows through every challenge.",
            BodyColor    = new Color(0.08f, 0.28f, 0.85f), // deep ocean blue
            AccentColor  = new Color(0.30f, 0.75f, 1.00f), // cyan shimmer
        },
        new ClassInfo
        {
            DisplayName  = "EARTH",
            Description  = "Grounded and enduring. The Earth avatar stands firm against all odds.",
            BodyColor    = new Color(0.22f, 0.42f, 0.08f), // forest green
            AccentColor  = new Color(0.55f, 0.85f, 0.20f), // lime highlight
        },
        new ClassInfo
        {
            DisplayName  = "AIR",
            Description  = "Swift and untouchable. The Air avatar moves faster than thought itself.",
            BodyColor    = new Color(0.72f, 0.72f, 0.88f), // pale lavender
            AccentColor  = new Color(0.95f, 0.95f, 1.00f), // white-silver shimmer
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
