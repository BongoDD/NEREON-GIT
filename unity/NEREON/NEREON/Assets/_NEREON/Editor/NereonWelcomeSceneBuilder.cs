#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Rebuilds WelcomeInitScene UI from scratch with a polished dark-fantasy layout.
/// Menu: NEREON → Build WelcomeInitScene
///
/// Creates:
///  • [Preview Stage] — off-screen camera + RenderTexture for 3D avatar portrait
///  • WelcomeCanvas — full 3-panel flow (Avatar Select → Name Entry → Confirming)
///  • FlowManager — WelcomeSceneFlow + WelcomeInitController + AvatarSelector, all wired
///
/// Safe to re-run — replaces existing canvas and flow manager each time.
/// </summary>
public static class NereonWelcomeSceneBuilder
{
    const string ScenePath = "Assets/_NEREON/Scenes/WelcomeInitScene.unity";
    const string RTPath    = "Assets/_NEREON/Data/AvatarPreviewRT.renderTexture";
    const string PreviewLayerName = "AvatarPreview";

    // ── Colour palette ────────────────────────────────────────────────────────
    static readonly Color C_BG       = new Color(0.04f, 0.04f, 0.08f, 1.00f);
    static readonly Color C_PANEL    = new Color(0.07f, 0.07f, 0.12f, 0.97f);
    static readonly Color C_FRAME    = new Color(0.12f, 0.12f, 0.20f, 1.00f);
    static readonly Color C_GOLD     = new Color(1.00f, 0.88f, 0.40f, 1.00f);
    static readonly Color C_WHITE    = new Color(0.88f, 0.88f, 0.93f, 1.00f);
    static readonly Color C_DIM      = new Color(0.55f, 0.55f, 0.65f, 1.00f);
    static readonly Color C_BTN      = new Color(0.14f, 0.14f, 0.24f, 1.00f);
    static readonly Color C_BTN_HI   = new Color(0.22f, 0.22f, 0.38f, 1.00f);
    static readonly Color C_BTN_PR   = new Color(0.08f, 0.08f, 0.14f, 1.00f);
    static readonly Color C_ACCENT   = new Color(0.42f, 0.28f, 0.82f, 1.00f);
    static readonly Color C_ACCENT_HI= new Color(0.55f, 0.38f, 0.95f, 1.00f);
    static readonly Color C_ERR      = new Color(0.95f, 0.35f, 0.35f, 1.00f);
    static readonly Color C_INPUT_BG = new Color(0.05f, 0.05f, 0.09f, 1.00f);
    static readonly Color C_INPUT_BD = new Color(0.30f, 0.28f, 0.55f, 1.00f);

    // ── Refs struct ───────────────────────────────────────────────────────────

    struct Refs
    {
        public CanvasGroup     FadeOverlay, PanelIntro, PanelAvatarSelect, PanelNameEntry, PanelConfirming;
        public Button          BtnBegin, BtnLeft, BtnRight, BtnNext, BtnBack, BtnConfirm;
        public TextMeshProUGUI ClassNameLabel, ClassDescLabel, PageIndicator;
        public TextMeshProUGUI CharCount, NameError, StatusText;
        public TMP_InputField  UsernameInput;
        public GameObject      Spinner;
    }

    // ── Entry point ───────────────────────────────────────────────────────────

    [MenuItem("NEREON/Build WelcomeInitScene")]
    public static void BuildWelcomeInitScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        int previewLayer = EnsureLayer(PreviewLayerName);

        // Clean slate
        DestroyNamed("WelcomeCanvas");
        DestroyNamed("[Preview Stage]");
        DestroyNamed("FlowManager");

        // Main camera setup
        SetupMainCamera(previewLayer);

        // Assets
        var rt            = EnsureRenderTexture();
        var previewAnchor = BuildPreviewStage(rt, previewLayer);

        // UI
        var refs = BuildCanvas(rt);

        // Logic components
        BuildFlowManager(refs, previewAnchor);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log("[NereonWelcomeSceneBuilder] ✔ WelcomeInitScene rebuilt. Press Play to test.");
        EditorUtility.DisplayDialog("Done", "WelcomeInitScene rebuilt.\nPress ▶ Play to test.", "OK");
    }

    // ── Preview stage ─────────────────────────────────────────────────────────

    static Transform BuildPreviewStage(RenderTexture rt, int previewLayer)
    {
        var stageGO = new GameObject("[Preview Stage]");
        stageGO.transform.position = new Vector3(500f, 0f, 500f);

        // Directional light
        var lightGO = new GameObject("PreviewLight");
        lightGO.transform.SetParent(stageGO.transform, false);
        lightGO.transform.SetPositionAndRotation(
            new Vector3(500f, 10f, 503f),
            Quaternion.Euler(45f, -30f, 0f));
        var dl = lightGO.AddComponent<Light>();
        dl.type      = LightType.Directional;
        dl.color     = new Color(1f, 0.92f, 0.78f);
        dl.intensity = 1.6f;

        // Preview anchor (character spawned here)
        var anchorGO = new GameObject("PreviewAnchor");
        anchorGO.transform.SetParent(stageGO.transform, false);
        anchorGO.transform.position = new Vector3(500f, 0f, 500f);

        // Preview camera
        var camGO = new GameObject("PreviewCamera");
        camGO.transform.SetParent(stageGO.transform, false);
        camGO.transform.SetPositionAndRotation(
            new Vector3(500f, 1.1f, 504.5f),
            Quaternion.Euler(0f, 180f, 0f));
        var cam             = camGO.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.04f, 0.08f, 1f);
        cam.cullingMask     = 1 << previewLayer;
        cam.targetTexture   = rt;
        cam.fieldOfView     = 38f;
        cam.nearClipPlane   = 0.1f;
        cam.farClipPlane    = 15f;
        cam.depth           = 1f;

        return anchorGO.transform;
    }

    // ── Canvas ────────────────────────────────────────────────────────────────

    static Refs BuildCanvas(RenderTexture rt)
    {
        var refs = new Refs();

        // Root canvas
        var canvasGO = new GameObject("WelcomeCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Full-screen dark background
        var bg = MakeFullStretch(canvasGO.transform, "Background");
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.color = C_BG;

        // EventSystem (only if one doesn't exist)
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Fade overlay (full-screen black, starts opaque for fade-in)
        refs.FadeOverlay       = BuildFadeOverlay(canvasGO.transform);

        // Four panels
        refs.PanelIntro        = BuildPanel_Intro(canvasGO.transform, ref refs);
        refs.PanelAvatarSelect = BuildPanel_AvatarSelect(canvasGO.transform, rt, ref refs);
        refs.PanelNameEntry    = BuildPanel_NameEntry(canvasGO.transform, ref refs);
        refs.PanelConfirming   = BuildPanel_Confirming(canvasGO.transform, ref refs);

        return refs;
    }

    // ── Fade overlay ──────────────────────────────────────────────────────────

    static CanvasGroup BuildFadeOverlay(Transform parent)
    {
        var rt  = MakeFullStretch(parent, "FadeOverlay");
        var img = rt.gameObject.AddComponent<Image>();
        img.color = Color.black;
        var cg  = rt.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 1f;  // starts black, fades out on entry
        cg.interactable = cg.blocksRaycasts = false;
        return cg;
    }

    // ── Panel: Intro ──────────────────────────────────────────────────────────

    static CanvasGroup BuildPanel_Intro(Transform parent, ref Refs refs)
    {
        var panel = MakeFullStretch(parent, "Panel_Intro");
        var cg    = panel.gameObject.AddComponent<CanvasGroup>();
        cg.alpha  = 0f;
        cg.interactable = cg.blocksRaycasts = false;

        // ── NEREON title ──
        MakeText(panel, "Logo", "NEREON", 88, C_GOLD,
            new Vector2(0, 350), new Vector2(900, 120), bold: true);

        MakeText(panel, "Tagline", "THE ELEMENTAL WORLD", 28, C_DIM,
            new Vector2(0, 278), new Vector2(700, 44));

        // ── Divider ──
        var div = MakeRT(panel, "Divider", new Vector2(0, 240), new Vector2(600, 2));
        var divImg = div.gameObject.AddComponent<Image>();
        divImg.color = new Color(0.35f, 0.28f, 0.65f, 0.7f);

        // ── Info blocks ──
        // Block 1 — Avatar
        MakeText(panel, "Block1Title", "FIRE · WATER · EARTH · AIR  —  CHOOSE YOUR ELEMENT", 22, C_WHITE,
            new Vector2(0, 185), new Vector2(860, 40), bold: true);
        var b1 = MakeText(panel, "Block1Body",
            "Your avatar is your identity in NEREON.\n" +
            "Fire · Water · Earth · Air — pick the element that calls to you.\n" +
            "Your element does not affect which mini-games you can play.\n" +
            "All games are open to all avatars.",
            21, C_DIM, new Vector2(0, 100), new Vector2(780, 100));
        b1.enableWordWrapping = true;
        b1.lineSpacing = 8;

        // Block 2 — Progression
        MakeText(panel, "Block2Title", ">>  GROW YOUR AVATAR", 26, C_WHITE,
            new Vector2(0, 25), new Vector2(860, 40), bold: true);
        var b2 = MakeText(panel, "Block2Body",
            "Level up, earn achievements and collect items as you play.\n" +
            "Your avatar gains unique visual effects, auras and cosmetic bonuses\n" +
            "that reflect your progress — all stored permanently on-chain.",
            21, C_DIM, new Vector2(0, -55), new Vector2(780, 90));
        b2.enableWordWrapping = true;
        b2.lineSpacing = 8;

        // Block 3 — Skill
        MakeText(panel, "Block3Title", ">>  SKILL WINS", 26, C_WHITE,
            new Vector2(0, -138), new Vector2(860, 40), bold: true);
        var b3 = MakeText(panel, "Block3Body",
            "Every mini-game in NEREON is purely skill-based.\n" +
            "No pay-to-win. No element advantage. The leaderboard rewards the best players.\n" +
            "Top 5 each month win real SPL tokens — automatically, by smart contract.",
            21, C_DIM, new Vector2(0, -220), new Vector2(780, 90));
        b3.enableWordWrapping = true;
        b3.lineSpacing = 8;

        // ── Element preview dots ──
        var dotPositions = new[] {
            new Vector2(-195, -330), new Vector2(-65, -330),
            new Vector2( 65, -330), new Vector2(195, -330)
        };
        var dotColors = new[] {
            new Color(0.9f, 0.3f, 0.05f),  // Fire
            new Color(0.1f, 0.4f, 0.95f),  // Water
            new Color(0.2f, 0.7f, 0.15f),  // Earth
            new Color(0.8f, 0.8f, 0.95f),  // Air
        };
        var dotLabels = new[] { "FIRE", "WATER", "EARTH", "AIR" };

        for (int i = 0; i < 4; i++)
        {
            var dot = MakeRT(panel, $"ElementDot_{dotLabels[i]}", dotPositions[i], new Vector2(90, 90));
            var dotImg = dot.gameObject.AddComponent<Image>();
            dotImg.color = dotColors[i];
            // Make it a circle using pixel-perfect unity circle trick with 
            // a squarish image — we'll note it's a placeholder colour swatch
            MakeText(dot, "Label", dotLabels[i], 15, Color.white,
                new Vector2(0, -60), new Vector2(110, 30), bold: true);
        }

        // ── Begin button ──
        refs.BtnBegin = MakeButton(panel, "BtnBegin", "BEGIN YOUR JOURNEY  →", 28,
            new Vector2(0, -450), new Vector2(340, 66),
            C_ACCENT, C_ACCENT_HI, C_BTN_PR, C_WHITE);

        return cg;
    }

    // ── Panel: Avatar Select ──────────────────────────────────────────────────

    static CanvasGroup BuildPanel_AvatarSelect(Transform parent, RenderTexture rt, ref Refs refs)
    {
        var panel = MakeFullStretch(parent, "Panel_AvatarSelect");
        var cg    = panel.gameObject.AddComponent<CanvasGroup>();
        cg.alpha  = 0f;
        cg.interactable = cg.blocksRaycasts = false;

        // Title
        MakeText(panel, "Title", "CHOOSE YOUR AVATAR", 52, C_GOLD,
            new Vector2(0, 370), new Vector2(900, 80), bold: true);

        // Subtitle / step indicator
        MakeText(panel, "StepLabel", "STEP 1 OF 2", 22, C_DIM,
            new Vector2(0, 310), new Vector2(400, 40));

        // Preview frame (border)
        var frame    = MakeRT(panel, "PreviewFrame", new Vector2(0, 20), new Vector2(360, 460));
        var frameImg = frame.gameObject.AddComponent<Image>();
        frameImg.color = C_FRAME;

        // RawImage inside frame
        var rawRT  = MakeRT(frame, "PreviewImage", Vector2.zero, new Vector2(344, 444));
        rawRT.anchorMin = rawRT.anchorMax = new Vector2(0.5f, 0.5f);
        var rawImg = rawRT.gameObject.AddComponent<RawImage>();
        rawImg.texture = rt;

        // Left arrow button
        refs.BtnLeft = MakeArrowButton(panel, "BtnLeft", "◄", new Vector2(-265, 20));

        // Right arrow button
        refs.BtnRight = MakeArrowButton(panel, "BtnRight", "►", new Vector2(265, 20));

        // Class name
        refs.ClassNameLabel = MakeText(panel, "ClassNameLabel", "WARRIOR", 46, C_WHITE,
            new Vector2(0, -225), new Vector2(700, 70), bold: true);

        // Class description
        refs.ClassDescLabel = MakeText(panel, "ClassDescLabel",
            "Fearless frontline fighter. High health, relentless aggression.",
            24, C_DIM, new Vector2(0, -295), new Vector2(600, 80));
        refs.ClassDescLabel.fontStyle = FontStyles.Italic;
        refs.ClassDescLabel.enableWordWrapping = true;

        // Dot page indicator
        refs.PageIndicator = MakeText(panel, "PageIndicator", "● ○ ○ ○", 32, C_WHITE,
            new Vector2(0, -360), new Vector2(400, 50));

        // NEXT button (accent coloured)
        refs.BtnNext = MakeButton(panel, "BtnNext", "NEXT  →", 30,
            new Vector2(0, -430), new Vector2(220, 62),
            C_ACCENT, C_ACCENT_HI, C_BTN_PR, C_WHITE);

        return cg;
    }

    // ── Panel: Name Entry ─────────────────────────────────────────────────────

    static CanvasGroup BuildPanel_NameEntry(Transform parent, ref Refs refs)
    {
        var panel = MakeFullStretch(parent, "Panel_NameEntry");
        var cg    = panel.gameObject.AddComponent<CanvasGroup>();
        cg.alpha  = 0f;
        cg.interactable = cg.blocksRaycasts = false;

        MakeText(panel, "Title", "ENTER YOUR NAME", 52, C_GOLD,
            new Vector2(0, 220), new Vector2(900, 80), bold: true);

        MakeText(panel, "StepLabel", "STEP 2 OF 2", 22, C_DIM,
            new Vector2(0, 160), new Vector2(400, 40));

        // Input field
        refs.UsernameInput = MakeInputField(panel, "UsernameInput",
            "Choose a name…", new Vector2(0, 40), new Vector2(520, 72));

        // Char counter (bottom-right of input)
        refs.CharCount = MakeText(panel, "CharCount", "0 / 20", 20, C_DIM,
            new Vector2(260, -10), new Vector2(120, 34));

        // Error label
        refs.NameError = MakeText(panel, "NameError", "", 22, C_ERR,
            new Vector2(0, -55), new Vector2(520, 40));

        // Back + Confirm buttons side by side
        refs.BtnBack    = MakeButton(panel, "BtnBack", "← BACK", 26,
            new Vector2(-145, -160), new Vector2(250, 62),
            C_BTN, C_BTN_HI, C_BTN_PR, C_WHITE);

        refs.BtnConfirm = MakeButton(panel, "BtnConfirm", "ENTER NEREON", 26,
            new Vector2(145, -160), new Vector2(250, 62),
            C_ACCENT, C_ACCENT_HI, C_BTN_PR, C_WHITE);
        refs.BtnConfirm.interactable = false; // disabled until valid name

        return cg;
    }

    // ── Panel: Confirming ─────────────────────────────────────────────────────

    static CanvasGroup BuildPanel_Confirming(Transform parent, ref Refs refs)
    {
        var panel = MakeFullStretch(parent, "Panel_Confirming");
        var cg    = panel.gameObject.AddComponent<CanvasGroup>();
        cg.alpha  = 0f;
        cg.interactable = cg.blocksRaycasts = false;

        MakeText(panel, "Title", "ENTERING NEREON", 52, C_GOLD,
            new Vector2(0, 140), new Vector2(900, 80), bold: true);

        // Spinner (rotating ring graphic using a circle Image with fill)
        var spinnerRT  = MakeRT(panel, "Spinner", new Vector2(0, 20), new Vector2(80, 80));
        var spinnerImg = spinnerRT.gameObject.AddComponent<Image>();
        spinnerImg.color     = C_ACCENT;
        spinnerImg.fillMethod = Image.FillMethod.Radial360;
        spinnerImg.type      = Image.Type.Filled;
        spinnerImg.fillAmount = 0.75f;
        spinnerRT.gameObject.AddComponent<UISpinner>();
        refs.Spinner = spinnerRT.gameObject;

        refs.StatusText = MakeText(panel, "StatusText",
            "Saving your profile to the blockchain…", 28, C_WHITE,
            new Vector2(0, -70), new Vector2(700, 60));

        return cg;
    }

    // ── FlowManager ───────────────────────────────────────────────────────────

    static void BuildFlowManager(Refs refs, Transform previewAnchor)
    {
        var go     = new GameObject("FlowManager");
        var flow   = go.AddComponent<WelcomeSceneFlow>();
        var txCtrl = go.AddComponent<WelcomeInitController>();
        var sel    = go.AddComponent<AvatarSelector>();
        go.AddComponent<NereonAuthGuard>(); // redirects to LandingScene if no wallet

        // Wire WelcomeSceneFlow
        Wire(flow, "_txController",      txCtrl);
        Wire(flow, "_avatarSelector",    sel);
        Wire(flow, "_panelIntro",        refs.PanelIntro);
        Wire(flow, "_panelAvatarSelect", refs.PanelAvatarSelect);
        Wire(flow, "_panelNameEntry",    refs.PanelNameEntry);
        Wire(flow, "_panelConfirming",   refs.PanelConfirming);
        Wire(flow, "_fadeOverlay",       refs.FadeOverlay);
        Wire(flow, "_btnBegin",          refs.BtnBegin);
        Wire(flow, "_btnNext",           refs.BtnNext);
        Wire(flow, "_usernameInput",     refs.UsernameInput);
        Wire(flow, "_charCountLabel",    refs.CharCount);
        Wire(flow, "_btnBack",           refs.BtnBack);
        Wire(flow, "_btnConfirm",        refs.BtnConfirm);
        Wire(flow, "_nameErrorLabel",    refs.NameError);
        Wire(flow, "_statusText",        refs.StatusText);
        Wire(flow, "_spinner",           refs.Spinner);

        // Wire AvatarSelector
        Wire(sel, "_previewStage",    previewAnchor);
        Wire(sel, "_leftButton",      refs.BtnLeft);
        Wire(sel, "_rightButton",     refs.BtnRight);
        Wire(sel, "_classNameLabel",  refs.ClassNameLabel);
        Wire(sel, "_classDescLabel",  refs.ClassDescLabel);
        Wire(sel, "_pageIndicator",   refs.PageIndicator);

        var registry = AssetDatabase.LoadAssetAtPath<AvatarRegistry>("Assets/_NEREON/Data/AvatarRegistry.asset");
        if (registry != null) Wire(sel, "_registry", registry);
    }

    // ── Helpers — UI factory ──────────────────────────────────────────────────

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
        tmp.text              = text;
        tmp.fontSize          = size;
        tmp.color             = color;
        tmp.alignment         = align;
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
        cb.colorMultiplier  = 1f;
        btn.colors = cb;
        btn.targetGraphic = img;

        // Label child (full stretch so text is centered)
        var lblRT  = rt.gameObject.AddComponent<RectTransform>(); // already has one
        var lblGO  = new GameObject("Label");
        var lblRt2 = lblGO.AddComponent<RectTransform>();
        lblRt2.SetParent(rt, false);
        lblRt2.anchorMin = Vector2.zero;
        lblRt2.anchorMax = Vector2.one;
        lblRt2.sizeDelta = Vector2.zero;
        lblRt2.anchoredPosition = Vector2.zero;
        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = fontSize;
        tmp.color     = textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        return btn;
    }

    static Button MakeArrowButton(Transform parent, string name, string arrow, Vector2 pos)
    {
        var btn = MakeButton(parent, name, arrow, 36,
            pos, new Vector2(72, 72),
            C_BTN, C_BTN_HI, C_BTN_PR, C_WHITE);
        return btn;
    }

    static TMP_InputField MakeInputField(Transform parent, string name,
        string placeholder, Vector2 pos, Vector2 size)
    {
        // Container
        var rt  = MakeRT(parent, name, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = C_INPUT_BG;

        // Border (child Image, slightly larger, behind)
        var border = MakeRT(rt, "Border", Vector2.zero, size + new Vector2(4, 4));
        border.SetAsFirstSibling();
        var borderImg = border.gameObject.AddComponent<Image>();
        borderImg.color = C_INPUT_BD;

        // TMP_InputField
        var field = rt.gameObject.AddComponent<TMP_InputField>();
        field.characterLimit = 20;

        // Text viewport
        var vpRT = new GameObject("Text Area").AddComponent<RectTransform>();
        vpRT.SetParent(rt, false);
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = new Vector2(16, 6);
        vpRT.offsetMax = new Vector2(-16, -6);
        vpRT.gameObject.AddComponent<RectMask2D>();
        field.textViewport = vpRT;

        // Placeholder
        var phRT  = new GameObject("Placeholder").AddComponent<RectTransform>();
        phRT.SetParent(vpRT, false);
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.sizeDelta = Vector2.zero; phRT.anchoredPosition = Vector2.zero;
        var phTMP = phRT.gameObject.AddComponent<TextMeshProUGUI>();
        phTMP.text       = placeholder;
        phTMP.fontSize   = 28f;
        phTMP.color      = C_DIM;
        phTMP.fontStyle  = FontStyles.Italic;
        phTMP.alignment  = TextAlignmentOptions.MidlineLeft;
        field.placeholder = phTMP;

        // Text
        var txtRT = new GameObject("Text").AddComponent<RectTransform>();
        txtRT.SetParent(vpRT, false);
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero; txtRT.anchoredPosition = Vector2.zero;
        var txtTMP = txtRT.gameObject.AddComponent<TextMeshProUGUI>();
        txtTMP.fontSize  = 30f;
        txtTMP.color     = C_WHITE;
        txtTMP.alignment = TextAlignmentOptions.MidlineLeft;
        field.textComponent = txtTMP;

        return field;
    }

    // ── Helpers — wiring ──────────────────────────────────────────────────────

    static void Wire(Component comp, string field, Object value)
    {
        var so   = new SerializedObject(comp);
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogWarning($"[Wire] '{field}' not found on {comp.GetType().Name}"); return; }
        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    // ── Helpers — scene ───────────────────────────────────────────────────────

    static void DestroyNamed(string goName)
    {
        var existing = GameObject.Find(goName);
        if (existing != null) Object.DestroyImmediate(existing);
    }

    static void SetupMainCamera(int previewLayer)
    {
        var cam = Camera.main;
        if (cam == null) return;
        if (previewLayer > 0)
            cam.cullingMask &= ~(1 << previewLayer);  // exclude preview layer
    }

    static RenderTexture EnsureRenderTexture()
    {
        EnsureDir("Assets/_NEREON/Data");
        var existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(RTPath);
        if (existing != null) return existing;

        var rt = new RenderTexture(512, 640, 16, RenderTextureFormat.ARGB32);
        rt.name = "AvatarPreviewRT";
        rt.Create();
        AssetDatabase.CreateAsset(rt, RTPath);
        AssetDatabase.SaveAssets();
        return rt;
    }

    static int EnsureLayer(string layerName)
    {
        var tm = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        for (int i = 0; i < layers.arraySize; i++)
        {
            var e = layers.GetArrayElementAtIndex(i);
            if (e.stringValue == layerName) return i;
        }
        for (int i = 8; i < layers.arraySize; i++)
        {
            var e = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(e.stringValue))
            {
                e.stringValue = layerName;
                tm.ApplyModifiedProperties();
                Debug.Log($"[NereonWelcomeSceneBuilder] Created layer '{layerName}' at index {i}");
                return i;
            }
        }
        Debug.LogWarning("[NereonWelcomeSceneBuilder] No free layer slot — preview camera will use Default layer.");
        return 0;
    }

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
