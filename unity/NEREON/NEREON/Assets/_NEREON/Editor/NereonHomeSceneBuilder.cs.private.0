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
/// Menu: NEREON → Build HomeScene
/// Safe to re-run — skips existing GOs.
/// </summary>
public static class NereonHomeSceneBuilder
{
    private const string ScenePath = "Assets/_NEREON/Scenes/HomeScene.unity";
    private const string GraphPath = "Assets/_NEREON/Data/NereonTerrainGraph.asset";

    [MenuItem("NEREON/Build HomeScene")]
    public static void BuildHomeScene()
    {
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

        Debug.Log("[NereonHomeSceneBuilder] HomeScene built. Wire Inspector references.");
    }

    // ── MapMagic ──────────────────────────────────────────────────────────────

    static void CreateOrGet_MapMagic()
    {
        if (GameObject.Find("MapMagic") != null)
        {
            Debug.Log("[Builder] MapMagic already exists — skipped.");
            return;
        }

        var go = new GameObject("MapMagic");
        var mm = go.AddComponent<MapMagicObject>();

        mm.tileSize        = new Den.Tools.Vector2D(200, 200);
        mm.mainRange       = 0;
        mm.tileResolution  = MapMagicObject.Resolution._257;
        mm.draftResolution = MapMagicObject.Resolution._65;
        mm.globals.height  = 50f;

        var graph = CreateTerrainGraph();
        mm.graph = graph;

        go.AddComponent<TerrainBuildingPlacer>();

        Debug.Log("[Builder] MapMagicObject created with 200x200 single-tile graph.");
    }

    static Graph CreateTerrainGraph()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Graph>(GraphPath);
        if (existing != null) return existing;

        EnsureDir("Assets/_NEREON/Data");

        var graph = ScriptableObject.CreateInstance<Graph>();

        var noise = (Noise200)Generator.Create(typeof(Noise200));
        noise.type        = Noise200.Type.Perlin;
        noise.seed        = 42;
        noise.intensity   = 0.45f;
        noise.size        = 150f;
        noise.detail      = 0.55f;
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

        var texOut = (TexturesOutput200)Generator.Create(typeof(TexturesOutput200));
        texOut.guiPosition = new Vector2(0, 100);
        graph.Add(texOut);

        AssetDatabase.CreateAsset(graph, GraphPath);
        AssetDatabase.SaveAssets();

        Debug.Log("[Builder] Terrain graph created. Assign TerrainLayers in MapMagic window.");
        return graph;
    }

    // ── Environment ───────────────────────────────────────────────────────────

    static void CreateOrGet_Environment()
    {
        var root = FindOrCreate(null, "[Environment]");

        var sun = FindOrCreate(root, "Sun");
        if (sun.GetComponent<Light>() == null)
        {
            var l = sun.AddComponent<Light>();
            l.type      = LightType.Directional;
            l.color     = new Color(1f, 0.95f, 0.8f);
            l.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        var river = FindOrCreate(root, "River");
        if (river.GetComponent<MeshFilter>() == null)
        {
            var mf = river.AddComponent<MeshFilter>();
            mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Plane.fbx");
            river.AddComponent<MeshRenderer>();
            river.transform.position   = new Vector3(90f, 0.1f, 0f);
            river.transform.localScale = new Vector3(2f, 1f, 20f);
        }

        var skyboxGO = FindOrCreate(root, "SkyboxManager");
        if (skyboxGO.GetComponent<SkyboxController>() == null)
            skyboxGO.AddComponent<SkyboxController>();

        Debug.Log("[Builder] [Environment] created.");
    }

    // ── Districts & Buildings ─────────────────────────────────────────────────

    static void CreateOrGet_Districts()
    {
        var root  = FindOrCreate(null, "[Districts]");
        var plaza = FindOrCreate(root, "CentralPlaza");

        FindOrCreate(plaza, "SpawnPoint").transform.position  = Vector3.zero;
        FindOrCreate(plaza, "Fountain").transform.position    = new Vector3(0f, 0f, 5f);
        FindOrCreate(plaza, "NoticeBoard").transform.position = new Vector3(-8f, 0f, 0f);

        var market = FindOrCreate(root, "MarketBazaar");
        CreateBuilding(market, "Building_01", 0, new Vector3(-55f, 0f, -25f));
        CreateBuilding(market, "Building_02", 1, new Vector3(-35f, 0f, -55f));

        var mystic = FindOrCreate(root, "MysticQuarter");
        CreateBuilding(mystic, "Building_03", 2, new Vector3(45f, 0f, 55f));
        CreateBuilding(mystic, "Building_04", 3, new Vector3(65f, 0f, 30f));

        var arena = FindOrCreate(root, "ChampionArena");
        CreateBuilding(arena, "Building_05", 4, new Vector3(55f, 0f, -50f));

        Debug.Log("[Builder] [Districts] created with 5 building placeholders.");
    }

    static void CreateBuilding(GameObject parent, string name, int gameId, Vector3 pos)
    {
        var go = FindOrCreate(parent, name);
        go.transform.position = pos;

        try { go.tag = "Building"; }
        catch { Debug.LogWarning($"[Builder] Tag 'Building' not in project — skipped on {name}."); }

        if (go.GetComponent<MeshFilter>() == null)
        {
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            go.AddComponent<MeshRenderer>();
            go.transform.localScale = new Vector3(6f, 5f, 6f);
        }

        if (go.GetComponent<BoxCollider>() == null)
        {
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = false;
        }

        if (go.GetComponent<BuildingInteraction>() == null)
        {
            go.AddComponent<BuildingInteraction>();
            Debug.Log($"[Builder] {name} — wire WorldConfig + set GameId={gameId} in Inspector.");
        }
    }

    // ── Player ────────────────────────────────────────────────────────────────

    static void CreateOrGet_Player()
    {
        var root = FindOrCreate(null, "[Player]");
        var sp   = FindOrCreate(root, "SpawnPoint");
        sp.transform.position = Vector3.zero;
        Debug.Log("[Builder] [Player] created.");
    }

    // ── Managers ──────────────────────────────────────────────────────────────

    static void CreateOrGet_Managers()
    {
        var root = FindOrCreate(null, "[Managers]");

        if (root.GetComponentInChildren<HomeSceneManager>() == null)
        {
            FindOrCreate(root, "HomeSceneManager").AddComponent<HomeSceneManager>();
        }

        if (FindObjectOfType<NetworkManager>() == null)
        {
            var nmGO = FindOrCreate(root, "NetworkManager");
            nmGO.AddComponent<NetworkManager>();
            nmGO.AddComponent<UnityTransport>();
            Debug.Log("[Builder] NetworkManager added — set Transport to UnityTransport.");
        }

        if (FindObjectOfType<NereonNetworkManager>() == null)
        {
            var nmGO = root.transform.Find("NetworkManager")?.gameObject
                       ?? FindOrCreate(root, "NetworkManager");
            nmGO.AddComponent<NereonNetworkManager>();
        }

        if (root.GetComponentInChildren<NereonAuthGuard>() == null)
        {
            FindOrCreate(root, "AuthGuard").AddComponent<NereonAuthGuard>();
        }

        Debug.Log("[Builder] [Managers] created.");
    }

    // ── HUD Canvas ────────────────────────────────────────────────────────────

    static void CreateOrGet_HUDCanvas()
    {
        if (GameObject.Find("[HUD Canvas]") != null) return;

        var canvasGO = new GameObject("[HUD Canvas]");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // FadeOverlay
        var fade   = new GameObject("FadeOverlay");
        fade.transform.SetParent(canvasGO.transform, false);
        var img    = fade.AddComponent<UnityEngine.UI.Image>();
        img.color  = Color.black;
        var fadeCG = fade.AddComponent<CanvasGroup>();
        fadeCG.alpha = 0f;
        var fadeRT   = fade.GetComponent<RectTransform>();
        fadeRT.anchorMin = Vector2.zero;
        fadeRT.anchorMax = Vector2.one;
        fadeRT.offsetMin = fadeRT.offsetMax = Vector2.zero;

        // PlayerHUD
        var hudGO = FindOrCreate(canvasGO, "PlayerHUD");
        if (hudGO.GetComponent<PlayerHUD>() == null)
            hudGO.AddComponent<PlayerHUD>();

        // ChatInputPanel — CanvasGroup FIRST, then ChatInputUI
        var chatPanel = FindOrCreate(canvasGO, "ChatInputPanel");
        var chatCG    = chatPanel.AddComponent<CanvasGroup>();
        chatCG.alpha          = 0f;
        chatCG.interactable   = false;
        chatCG.blocksRaycasts = false;
        chatPanel.AddComponent<ChatInputUI>();

        Debug.Log("[Builder] [HUD Canvas] created.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static GameObject FindOrCreate(GameObject parent, string name)
    {
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
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts   = path.Split('/');
        var current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static T FindObjectOfType<T>() where T : Object
        => Object.FindFirstObjectByType<T>();
}
#endif
