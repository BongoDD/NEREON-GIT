#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates four elemental PlayerArmature avatar prefabs (Fire/Water/Earth/Air)
/// from the Starter Assets PlayerArmature, then populates AvatarRegistry.asset.
///
/// Menu: NEREON → Build Avatar Prefabs
///
/// What it does:
///  1. Loads PlayerArmature.prefab from Starter Assets
///  2. For each element, duplicates the 3 armature materials with the element tint
///  3. Saves a copy of the prefab to Assets/_NEREON/Avatars/ with those materials
///  4. Adds NereonAvatarSetup component with element + colors
///  5. Creates/updates AvatarRegistry.asset with all 4 entries
/// </summary>
public static class NereonAvatarPrefabBuilder
{
    const string SourcePrefabPath  = "Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab";
    const string SourceMatArms     = "Assets/StarterAssets/ThirdPersonController/Character/Materials/M_Armature_Arms.mat";
    const string SourceMatBody     = "Assets/StarterAssets/ThirdPersonController/Character/Materials/M_Armature_Body.mat";
    const string SourceMatLegs     = "Assets/StarterAssets/ThirdPersonController/Character/Materials/M_Armature_Legs.mat";
    const string OutPrefabDir      = "Assets/_NEREON/Avatars";
    const string OutMatDir         = "Assets/_NEREON/Avatars/Materials";
    const string RegistryPath      = "Assets/_NEREON/Data/AvatarRegistry.asset";

    // ── Element definitions ────────────────────────────────────────────────────

    struct ElementDef
    {
        public string                     Name;
        public string                     Description;
        public byte                       AvatarId;
        public NereonAvatarSetup.Element  Element;
        public Color                      BodyColor;
        public Color                      AccentColor;
        public Color                      LegsColor;
    }

    static readonly ElementDef[] Elements =
    {
        new ElementDef
        {
            Name        = "Fire",
            AvatarId    = 0,
            Element     = NereonAvatarSetup.Element.Fire,
            Description = "Fierce and relentless. Burns with passion and raw power.",
            BodyColor   = new Color(0.80f, 0.15f, 0.04f),   // deep ember red
            AccentColor = new Color(1.00f, 0.48f, 0.04f),   // orange glow
            LegsColor   = new Color(0.55f, 0.08f, 0.02f),   // dark crimson
        },
        new ElementDef
        {
            Name        = "Water",
            AvatarId    = 1,
            Element     = NereonAvatarSetup.Element.Water,
            Description = "Fluid and adaptive. Flows through every challenge.",
            BodyColor   = new Color(0.06f, 0.25f, 0.82f),   // deep ocean blue
            AccentColor = new Color(0.28f, 0.72f, 1.00f),   // cyan shimmer
            LegsColor   = new Color(0.04f, 0.14f, 0.55f),   // deep navy
        },
        new ElementDef
        {
            Name        = "Earth",
            AvatarId    = 2,
            Element     = NereonAvatarSetup.Element.Earth,
            Description = "Grounded and enduring. Stands firm against all odds.",
            BodyColor   = new Color(0.20f, 0.42f, 0.07f),   // forest green
            AccentColor = new Color(0.52f, 0.85f, 0.18f),   // lime highlight
            LegsColor   = new Color(0.12f, 0.26f, 0.04f),   // dark moss
        },
        new ElementDef
        {
            Name        = "Air",
            AvatarId    = 3,
            Element     = NereonAvatarSetup.Element.Air,
            Description = "Swift and untouchable. Moves faster than thought.",
            BodyColor   = new Color(0.70f, 0.70f, 0.88f),   // pale lavender
            AccentColor = new Color(0.95f, 0.95f, 1.00f),   // white-silver shimmer
            LegsColor   = new Color(0.45f, 0.45f, 0.62f),   // slate blue
        },
    };

    // ── Entry point ───────────────────────────────────────────────────────────

    [MenuItem("NEREON/Build Avatar Prefabs")]
    public static void BuildAvatarPrefabs()
    {
        // Validate source assets
        if (!File.Exists(Path.Combine(Application.dataPath[..^"Assets".Length], SourcePrefabPath)))
        {
            EditorUtility.DisplayDialog("Missing Asset",
                $"Could not find:\n{SourcePrefabPath}\n\nImport Starter Assets – Third Person Character first.", "OK");
            return;
        }

        EnsureDir(OutPrefabDir);
        EnsureDir(OutMatDir);
        EnsureDir("Assets/_NEREON/Data");

        var srcMatArms = AssetDatabase.LoadAssetAtPath<Material>(SourceMatArms);
        var srcMatBody = AssetDatabase.LoadAssetAtPath<Material>(SourceMatBody);
        var srcMatLegs = AssetDatabase.LoadAssetAtPath<Material>(SourceMatLegs);

        var prefabPaths = new string[Elements.Length];

        for (int i = 0; i < Elements.Length; i++)
        {
            var def = Elements[i];
            var mats = CreateElementMaterials(def, srcMatArms, srcMatBody, srcMatLegs);
            prefabPaths[i] = CreateElementPrefab(def, mats);
            Debug.Log($"[NereonAvatarPrefabBuilder] Created {def.Name} avatar at {prefabPaths[i]}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        CreateOrUpdateRegistry(prefabPaths);

        EditorUtility.DisplayDialog("Done",
            "4 elemental avatar prefabs created:\n" +
            "  Assets/_NEREON/Avatars/\n\n" +
            "AvatarRegistry.asset updated.\n\n" +
            "Run  NEREON → Build HomeScene  to wire them in the scene.", "OK");
    }

    // ── Material creation ─────────────────────────────────────────────────────

    struct ElementMats { public Material Arms, Body, Legs; }

    static ElementMats CreateElementMaterials(ElementDef def,
        Material srcArms, Material srcBody, Material srcLegs)
    {
        return new ElementMats
        {
            Arms = DuplicateMat(srcArms, $"{def.Name}_Arms", def.BodyColor,  def.AccentColor),
            Body = DuplicateMat(srcBody, $"{def.Name}_Body", def.BodyColor,  def.AccentColor),
            Legs = DuplicateMat(srcLegs, $"{def.Name}_Legs", def.LegsColor,  def.AccentColor),
        };
    }

    static Material DuplicateMat(Material src, string assetName, Color baseColor, Color emissive)
    {
        var path = $"{OutMatDir}/{assetName}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

        Material mat;
        if (existing != null)
        {
            mat = existing;
        }
        else
        {
            mat = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.color = baseColor;

        // Enable emissive for the glow effect
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emissive * 0.35f);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ── Prefab creation ───────────────────────────────────────────────────────

    static string CreateElementPrefab(ElementDef def, ElementMats mats)
    {
        var outPath = $"{OutPrefabDir}/{def.Name}_Avatar.prefab";

        // Load a fresh editable copy of the source prefab
        var prefabGO = PrefabUtility.LoadPrefabContents(SourcePrefabPath);

        // Apply elemental materials to every SkinnedMeshRenderer
        foreach (var smr in prefabGO.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true))
        {
            var sharedMats = smr.sharedMaterials;
            for (int m = 0; m < sharedMats.Length; m++)
            {
                var name = sharedMats[m] != null ? sharedMats[m].name.ToLower() : "";
                if (name.Contains("arm"))       sharedMats[m] = mats.Arms;
                else if (name.Contains("leg"))  sharedMats[m] = mats.Legs;
                else                            sharedMats[m] = mats.Body;
            }
            smr.sharedMaterials = sharedMats;
        }

        // Also tint any regular MeshRenderers (hands, accessories)
        foreach (var mr in prefabGO.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
        {
            var sharedMats = mr.sharedMaterials;
            for (int m = 0; m < sharedMats.Length; m++)
                sharedMats[m] = mats.Body;
            mr.sharedMaterials = sharedMats;
        }

        // Add NereonAvatarSetup
        var existing = prefabGO.GetComponent<NereonAvatarSetup>();
        var setup    = existing != null ? existing : prefabGO.AddComponent<NereonAvatarSetup>();
        setup.AvatarElement = def.Element;
        setup.BodyColor     = def.BodyColor;
        setup.AccentColor   = def.AccentColor;

        // Save
        PrefabUtility.SaveAsPrefabAsset(prefabGO, outPath);
        PrefabUtility.UnloadPrefabContents(prefabGO);

        return outPath;
    }

    // ── AvatarRegistry ────────────────────────────────────────────────────────

    static void CreateOrUpdateRegistry(string[] prefabPaths)
    {
        var registry = AssetDatabase.LoadAssetAtPath<AvatarRegistry>(RegistryPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<AvatarRegistry>();
            AssetDatabase.CreateAsset(registry, RegistryPath);
        }

        registry.Avatars = new AvatarEntry[Elements.Length];
        for (int i = 0; i < Elements.Length; i++)
        {
            var def    = Elements[i];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);
            registry.Avatars[i] = new AvatarEntry
            {
                AvatarId    = def.AvatarId,
                DisplayName = def.Name.ToUpper(),
                Description = def.Description,
                Prefab      = prefab,
            };
        }

        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        Debug.Log("[NereonAvatarPrefabBuilder] AvatarRegistry updated with 4 elements.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void EnsureDir(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        var cur   = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
#endif
