#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Rebuilds LandingScene UI from scratch with a polished dark-fantasy layout.
/// Menu: NEREON → Build LandingScene
///
/// Layout:
///   • Full-screen dark background
///   • NEREON logo + tagline
///   • Four elemental accent bars (Fire / Water / Earth / Air)
///   • "ENTER NEREON" button — accent purple, disabled until wallet connected
///   • Wallet connection panel below (SDK wallet prefab renders on top)
///   • Status label showing connection state
///
/// Safe to re-run — replaces LandingCanvas and LoginManager each time.
/// The Web3 / SDK WalletHolder GameObject is not touched.
/// </summary>
public static class NereonLandingSceneBuilder
{
    const string ScenePath = "Assets/_NEREON/Scenes/LandingScene.unity";

    // ── Colour palette (matches WelcomeInitScene) ─────────────────────────────
    static readonly Color C_BG        = new Color(0.04f, 0.04f, 0.08f, 1.00f);
    static readonly Color C_PANEL     = new Color(0.07f, 0.07f, 0.12f, 0.97f);
    static readonly Color C_FRAME     = new Color(0.10f, 0.10f, 0.18f, 0.95f);
    static readonly Color C_GOLD      = new Color(1.00f, 0.88f, 0.40f, 1.00f);
    static readonly Color C_WHITE     = new Color(0.88f, 0.88f, 0.93f, 1.00f);
    static readonly Color C_DIM       = new Color(0.50f, 0.50f, 0.60f, 1.00f);
    static readonly Color C_BTN       = new Color(0.14f, 0.14f, 0.24f, 1.00f);
    static readonly Color C_BTN_HI    = new Color(0.22f, 0.22f, 0.38f, 1.00f);
    static readonly Color C_BTN_PR    = new Color(0.08f, 0.08f, 0.14f, 1.00f);
    static readonly Color C_ACCENT    = new Color(0.42f, 0.28f, 0.82f, 1.00f);
    static readonly Color C_ACCENT_HI = new Color(0.55f, 0.38f, 0.95f, 1.00f);
    static readonly Color C_ACCENT_DI = new Color(0.20f, 0.14f, 0.38f, 1.00f); // disabled
    static readonly Color C_DIVIDER   = new Color(0.30f, 0.24f, 0.55f, 0.60f);

    static readonly Color C_FIRE  = new Color(0.95f, 0.32f, 0.05f, 1f);
    static readonly Color C_WATER = new Color(0.12f, 0.45f, 0.95f, 1f);
    static readonly Color C_EARTH = new Color(0.22f, 0.72f, 0.18f, 1f);
    static readonly Color C_AIR   = new Color(0.78f, 0.82f, 0.98f, 1f);

    // ── Entry point ───────────────────────────────────────────────────────────

    [MenuItem("NEREON/Build LandingScene")]
    public static void BuildLandingScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        // Only remove our additions — never touch the existing SDK wallet UI
        DestroyNamed("LandingCanvas");
        DestroyNamed("LoginManager");

        // Find the WalletHolder so we can place ENTER NEREON just above it
        var walletHolder = GameObject.Find("WalletHolder");
        if (walletHolder == null)
            Debug.LogWarning("[NereonLandingSceneBuilder] WalletHolder not found — button will be center-screen.");

        var (btnEnter, statusLabel) = BuildEnterButton(walletHolder);
        BuildLoginManager(btnEnter, statusLabel);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log("[NereonLandingSceneBuilder] ✔ ENTER NEREON button added to LandingScene.");
        EditorUtility.DisplayDialog("Done",
            "ENTER NEREON button added above the WalletHolder.\n\n" +
            "Disabled until wallet connects, then turns accent purple.\n" +
            "Press ▶ Play to test.", "OK");
    }

    // ── Build the ENTER NEREON button above the WalletHolder ─────────────────

    static (Button btnEnter, TextMeshProUGUI statusLabel) BuildEnterButton(GameObject walletHolder)
    {
        // Find or create a canvas to host our button
        // Prefer the same parent canvas as WalletHolder so it stays visually grouped
        Canvas targetCanvas = null;
        Vector2 walletAnchoredPos = new Vector2(0, -80); // fallback

        if (walletHolder != null)
        {
            // Walk up to find the parent Canvas
            var t = walletHolder.transform.parent;
            while (t != null)
            {
                var c = t.GetComponent<Canvas>();
                if (c != null) { targetCanvas = c; break; }
                t = t.parent;
            }

            // Get wallet's anchored position so we can sit above it
            var walletRT = walletHolder.GetComponent<RectTransform>();
            if (walletRT != null)
                walletAnchoredPos = walletRT.anchoredPosition;
        }

        // If no canvas found (WalletHolder is at root or has its own), create one
        if (targetCanvas == null)
        {
            var canvasGO = new GameObject("LandingCanvas");
            targetCanvas = canvasGO.AddComponent<Canvas>();
            targetCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            targetCanvas.sortingOrder = 200;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();
        }
        else
        {
            // Reuse the wallet's canvas — name our root so we can clean it up later
            var go = new GameObject("LandingCanvas");
            go.AddComponent<RectTransform>().SetParent(targetCanvas.transform, false);
        }

        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Parent for our new elements — either the canvas root or the "LandingCanvas" child
        Transform root = targetCanvas.transform.Find("LandingCanvas") ?? targetCanvas.transform;

        // Button sits directly above the wallet holder
        float btnY = walletAnchoredPos.y + 90f;

        // ── Status label ──────────────────────────────────────────────────────
        var statusLabel = MakeText(root, "WalletStatusLabel",
            "Connect your wallet to continue",
            20, new Color(0.50f, 0.50f, 0.62f, 1f),
            new Vector2(walletAnchoredPos.x, btnY + 46f), new Vector2(400, 34),
            align: TextAlignmentOptions.Center);

        // ── ENTER NEREON button ───────────────────────────────────────────────
        var btnEnter = MakeButton(root, "BtnEnterNereon", "ENTER  NEREON", 26,
            new Vector2(walletAnchoredPos.x, btnY), new Vector2(300, 62),
            C_ACCENT_DI, C_ACCENT_HI, C_BTN_PR, C_WHITE);
        btnEnter.interactable = false;

        // Thin accent line between our button and the wallet button
        var sep = MakeRT(root, "WalletSep",
            new Vector2(walletAnchoredPos.x, btnY - 42f), new Vector2(300, 1));
        sep.gameObject.AddComponent<Image>().color = C_DIVIDER;

        return (btnEnter, statusLabel);
    }

    // ── LoginManager ──────────────────────────────────────────────────────────

    static void BuildLoginManager(Button btnEnter, TextMeshProUGUI statusLabel)
    {
        var go   = new GameObject("LoginManager");
        var ctrl = go.AddComponent<LoginFlowController>();

        Wire(ctrl, "_btnEnterNereon", btnEnter);
        Wire(ctrl, "_walletStatusLabel", statusLabel);
    }

    // ── UI Helpers ────────────────────────────────────────────────────────────

    static RectTransform MakeRT(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        return rt;
    }

    static RectTransform MakeFullStretch(Transform parent, string name)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, string text,
        float size, Color color, Vector2 pos, Vector2 sizeDelta,
        bool bold = false, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var rt  = MakeRT(parent, name, pos, sizeDelta);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text               = text;
        tmp.fontSize           = size;
        tmp.color              = color;
        tmp.alignment          = align;
        tmp.enableWordWrapping = false;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        return tmp;
    }

    static Button MakeButton(Transform parent, string name, string label, float fontSize,
        Vector2 pos, Vector2 size,
        Color bgNormal, Color bgHover, Color bgPressed, Color textColor)
    {
        var rt  = MakeRT(parent, name, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = bgNormal;

        var btn = rt.gameObject.AddComponent<Button>();
        var cb  = btn.colors;
        cb.normalColor      = bgNormal;
        cb.highlightedColor = bgHover;
        cb.pressedColor     = bgPressed;
        cb.selectedColor    = bgNormal;
        cb.disabledColor    = new Color(bgNormal.r * 0.5f, bgNormal.g * 0.5f, bgNormal.b * 0.5f, 0.5f);
        cb.colorMultiplier  = 1f;
        btn.colors = cb;
        btn.targetGraphic = img;

        var lblGO  = new GameObject("Label");
        var lblRt  = lblGO.AddComponent<RectTransform>();
        lblRt.SetParent(rt, false);
        lblRt.anchorMin = Vector2.zero;
        lblRt.anchorMax = Vector2.one;
        lblRt.sizeDelta = Vector2.zero;
        lblRt.anchoredPosition = Vector2.zero;
        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = fontSize;
        tmp.color     = textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        return btn;
    }

    // ── Scene helpers ─────────────────────────────────────────────────────────

    static void Wire(Component comp, string field, Object value)
    {
        var so   = new SerializedObject(comp);
        var prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogWarning($"[Wire] '{field}' not found on {comp.GetType().Name}");
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    static void DestroyNamed(string goName)
    {
        var existing = GameObject.Find(goName);
        if (existing != null) Object.DestroyImmediate(existing);
    }
}
#endif
