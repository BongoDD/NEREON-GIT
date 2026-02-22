#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using MapMagic.Core;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;

/// <summary>
/// One-click scene builder for HomeScene.
///
/// Menu: NEREON → Build HomeScene
///
/// Creates the full GameObject hierarchy, adds all required components,
/// and creates + assigns a starter MapMagic 2 graph asset.
/// Run once on a blank HomeScene — safe to re-run (skips existing GOs).
/// </summary>
public static class NereonHomeSceneBuilder
{
    private const string ScenePath  = "Assets/_NEREON/Scenes/HomeScene.unity";
    private const string GraphPath  = "Assets/_NEREON/Data/NereonTerrainGraph.asset";

    // ── Menu Entry ────────────────────────────────────────────────────────────

    [MenuItem("NEREON/Build HomeScene")]
    public static void BuildHomeScene()
    {
        // Open HomeScene if not already open
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        CreateOrGet_MapMagic();
        CreateOrGet_Environment();
        CreateOrGet_Districts();
        CreateOrGet_Player();
        CreateOrGet_Managers();
        CreateOrGet_HUDCanvas();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log("[NereonHomeSceneBuilder] HomeScene built. Open each section and wire Inspector references.");
    }

    // ── MapMagic ──────────────────────────────────────────────────────────────

    static void CreateOrGet_MapMagic()
    {
        var existing = GameObject.Find("MapMagic");
        if (existing != null) { Debug.Log("[Builder] MapMagic already exists — skipped."); return; }

        var go = new GameObject("MapMagic");
        var mm = go.AddComponent<MapMagicObject>();

        // Single 200×200 tile centred on origin — no infinite expansion
        mm.tileSize       = new Den.Tools.Vector2D(200, 200);
        mm.mainRange      = 0;   // 0 = single centre tile only
        mm.tileResolution = MapMagicObject.Resolution._257;
        mm.draftResolution = MapMagicObject.Resolution._65;

        // Terrain height (world units — determines max hill height)
        mm.globals.height = 50f;

        // Create and assign graph
        var graph = CreateTerrainGraph();
        mm.graph = graph;

        // TerrainBuildingPlacer on the same GO
        go.AddComponent<TerrainBuildingPlacer>();

        Debug.Log("[Builder] MapMagicObject created with 200×200 single-tile graph.");
    }

    static Graph CreateTerrainGraph()
    {
        // Reuse existing asset if already built
        var existing = AssetDatabase.LoadAssetAtPath<Graph>(GraphPath);
        if (existing != null) return existing;

        EnsureDir("Assets/_NEREON/Data");

        var graph = ScriptableObject.CreateInstance<Graph>();

        // ── Noise → Curve → Erosion → HeightOutput ──────────────────────────
        var noise = (Noise200)Generator.Create(typeof(Noise200));
        noise.type      = Noise200.Type.Perlin;
        noise.seed      = 42;
        noise.intensity = 0.45f;   // gentle hills
        noise.size      = 150f;
        noise.detail    = 0.55f;
        noise.guiPosition = new Vector2(-400, -100);
        graph.Add(noise);

        var erosion = (Erosion200)Generator.Create(typeof(Erosion200));
        erosion.guiPosition = new Vector2(-200, -100);
        graph.Add(erosion);
        graph.Link(erosion, noise);

        var heightOut = (HeightOutput200)Generator.Create(typeof(HeightOutput200));
        heightOut.guiPosition = new Vector2(0, -100);
        graph.Add(heightOut);
        graph.Link(heightOut, erosion);

        // ── Flat Noise (low intensity) → TexturesOutput ──────────────────────
        // TexturesOutput needs TerrainLayer assets — we leave layers empty for now.
        // Developer: open the graph, add 3 layers and assign Grass/Dirt/Stone TerrainLayers.
        var texOut = (TexturesOutput200)Generator.Create(typeof(TexturesOutput200));
        texOut.guiPosition = new Vector2(0, 100);
        graph.Add(texOut);

        AssetDatabase.CreateAsset(graph, GraphPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Builder] Terrain graph saved at {GraphPath}. " +
                  "Open it in MapMagic window and assign TerrainLayers to TexturesOutput.");
        return graph;
    }

    // ── Environment ───────────────────────────────────────────────────────────

    static void CreateOrGet_Environment()
    {
        var root = FindOrCreate(null, "[Environment]");

        // Sun (directional light)
        var sun = FindOrCreate(root, "Sun");
        if (sun.GetComponent<Light>() == null)
        {
            var l = sun.AddComponent<Light>();
            l.type      = LightType.Directional;
            l.color     = new Color(1f, 0.95f, 0.8f); // warm golden
            l.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        // River (flat water plane on east side)
        var river = FindOrCreate(root, "River");
        if (river.GetComponent<MeshFilter>() == null)
        {
            var mf = river.AddComponent<MeshFilter>();
            mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Plane.fbx");
            river.AddComponent<MeshRenderer>();
            river.transform.position   = new Vector3(90f, 0.1f, 0f);
            river.transform.localScale = new Vector3(2f, 1f, 20f);  // 20×200 strip on east boundary
        }

        // SkyboxController placeholder GO
        var skyboxGO = FindOrCreate(root, "SkyboxManager");
        if (skyboxGO.GetComponent<SkyboxController>() == null)
            skyboxGO.AddComponent<SkyboxController>();

        Debug.Log("[Builder] [Environment] created.");
    }

    // ── Districts & Buildings ─────────────────────────────────────────────────

    static void CreateOrGet_Districts()
    {
        var root = FindOrCreate(null, "[Districts]");

        // Central Plaza
        var plaza = FindOrCreate(root, "CentralPlaza");
        FindOrCreate(plaza, "SpawnPoint").transform.position = Vector3.zero;
        FindOrCreate(plaza, "Fountain").transform.position   = new Vector3(0f, 0f, 5f);
        FindOrCreate(plaza, "NoticeBoard").transform.position = new Vector3(-8f, 0f, 0f);

        // Market Bazaar (west, buildings 0 & 1)
        var market = FindOrCreate(root, "MarketBazaar");
        CreateBuilding(market, "Building_01", 0, new Vector3(-55f, 0f, -25f));
        CreateBuilding(market, "Building_02", 1, new Vector3(-35f, 0f, -55f));

        // Mystic Quarter (north-east, buildings 2 & 3)
        var mystic = FindOrCreate(root, "MysticQuarter");
        CreateBuilding(mystic, "Building_03", 2, new Vector3(45f, 0f, 55f));
        CreateBuilding(mystic, "Building_04", 3, new Vector3(65f, 0f, 30f));

        // Champion Arena (south-east, building 4)
        var arena = FindOrCreate(root, "ChampionArena");
        CreateBuilding(arena, "Building_05", 4, new Vector3(55f, 0f, -50f));

        Debug.Log("[Builder] [Districts] created with 5 building placeholders.");
    }

    static void CreateBuilding(GameObject parent, string name, int gameId, Vector3 pos)
    {
        var go = FindOrCreate(parent, name);
        go.transform.position = pos;
        go.tag = "Building";

        // Placeholder visual: scaled cube
        if (go.GetComponent<MeshFilter>() == null)
        {
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            go.AddComponent<MeshRenderer>();
            go.transform.localScale = new Vector3(6f, 5f, 6f);
        }

        // BoxCollider for trigger zone (BuildingInteraction uses it)
        if (go.GetComponent<BoxCollider>() == null)
        {
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = false;
        }

        // BuildingInteraction component
        if (go.GetComponent<BuildingInteraction>() == null)
        {
            var bi = go.AddComponent<BuildingInteraction>();
            // NOTE: Wire WorldConfig in Inspector after creating WorldConfig.asset
            Debug.Log($"[Builder] {name} — remember to wire WorldConfig & set GameId={gameId} in BuildingInteraction Inspector.");
        }
    }

    // ── Player ────────────────────────────────────────────────────────────────

    static void CreateOrGet_Player()
    {
        var root = FindOrCreate(null, "[Player]");

        // Spawn point marker (TerrainBuildingPlacer will snap Y to terrain)
        var sp = FindOrCreate(root, "SpawnPoint");
        sp.transform.position = Vector3.zero;

        Debug.Log("[Builder] [Player] created. Wire SpawnPoint to TerrainBuildingPlacer + AvatarManager.");
    }

    // ── Managers ──────────────────────────────────────────────────────────────

    static void CreateOrGet_Managers()
    {
        var root = FindOrCreate(null, "[Managers]");

        // HomeSceneManager
        if (root.GetComponentInChildren<HomeSceneManager>() == null)
        {
            var go = FindOrCreate(root, "HomeSceneManager");
            go.AddComponent<HomeSceneManager>();
        }

        // NetworkManager (NGO)
        if (FindObjectOfType<NetworkManager>() == null)
        {
            var nmGO = FindOrCreate(root, "NetworkManager");
            nmGO.AddComponent<NetworkManager>();
            nmGO.AddComponent<UnityTransport>();
            Debug.Log("[Builder] NetworkManager added — set Transport to UnityTransport in Inspector.");
        }

        // NereonNetworkManager
        if (FindObjectOfType<NereonNetworkManager>() == null)
        {
            var nmGO = root.transform.Find("NetworkManager")?.gameObject
                       ?? FindOrCreate(root, "NetworkManager");
            nmGO.AddComponent<NereonNetworkManager>();
        }

        // AuthGuard — redirects to LandingScene if wallet not connected
        if (root.GetComponentInChildren<NereonAuthGuard>() == null)
        {
            var go = FindOrCreate(root, "AuthGuard");
            go.AddComponent<NereonAuthGuard>();
        }

        Debug.Log("[Builder] [Managers] created.");
    }

    // ── HUD Canvas ────────────────────────────────────────────────────────────

    static void CreateOrGet_HUDCanvas()
    {
        var existing = GameObject.Find("[HUD Canvas]");
        if (existing != null) return;

        var canvasGO = new GameObject("[HUD Canvas]");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // FadeOverlay (full-screen black image for scene transitions)
        var fade = new GameObject("FadeOverlay");
        fade.transform.SetParent(canvasGO.transform, false);
        var img  = fade.AddComponent<UnityEngine.UI.Image>();
        img.color = Color.black;
        var cg   = fade.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        var rt   = fade.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        // PlayerHUD root GO
        var hudGO = FindOrCreate(canvasGO, "PlayerHUD");
        if (hudGO.GetComponent<PlayerHUD>() == null)
            hudGO.AddComponent<PlayerHUD>();

        // ChatInputPanel (hidden by default — ChatInputUI controls visibility)
        var chatPanel = FindOrCreate(canvasGO, "ChatInputPanel");
        var chatCG    = chatPanel.GetComponent<CanvasGroup>() ?? chatPanel.AddComponent<CanvasGroup>();
        chatCG.alpha          = 0f;
        chatCG.interactable   = false;
        chatCG.blocksRaycasts = false;
        if (chatPanel.GetComponent<ChatInputUI>() == null)
            chatPanel.AddComponent<ChatInputUI>();

        Debug.Log("[Builder] [HUD Canvas] created. Add TMP_InputField child to ChatInputPanel.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static GameObject FindOrCreate(GameObject parent, string name)
    {
        // Search in parent first, then scene-wide
        Transform found = parent != null
            ? parent.transform.Find(name)
            : GameObject.Find(name)?.transform;

        if (found != null) return found.gameObject;

        var go = new GameObject(name);
        if (parent != null)
            go.transform.SetParent(parent.transform, false);
        return go;
    }

    static void EnsureDir(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }

    // Convenience wrapper for FindObjectOfType (suppresses obsolete warning in older Unity)
    static T FindObjectOfType<T>() where T : Object
        => Object.FindFirstObjectByType<T>();
}
#endif
