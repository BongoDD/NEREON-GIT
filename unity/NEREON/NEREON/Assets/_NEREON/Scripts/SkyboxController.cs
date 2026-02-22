using System;
using System.Collections;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Generates skyboxes at runtime using the Blockade Labs Skybox AI REST API.
///
/// SETUP
/// -----
/// 1. Import the Blockade Labs package from the Asset Store (it adds the Editor
///    window for offline generation).  This script handles runtime / in-game
///    skybox generation independently of that editor tool.
/// 2. Create a SkyboxSettings ScriptableObject (right-click → NEREON → SkyboxSettings)
///    and enter your API key there — never hard-code the key in source files.
/// 3. Attach this script to a persistent GameObject (e.g. GameManager) in HomeScene.
///
/// USAGE
/// -----
///    // Generate a skybox from a text prompt and apply it to the scene:
///    await SkyboxController.Instance.GenerateAndApplyAsync("cute anime fantasy town");
/// </summary>
public class SkyboxController : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static SkyboxController Instance { get; private set; }

    // ── Inspector / Settings ──────────────────────────────────────────────────
    [Header("Blockade Labs API")]
    [Tooltip("Your Blockade Labs API key.  Get one at https://skybox.blockadelabs.com")]
    [SerializeField] private string _apiKey = "";

    [Tooltip("Style preset ID. 0 = auto (AI chooses). See Blockade Labs docs for full list.")]
    [SerializeField] private int _skyboxStyleId = 0;

    [Tooltip("How often (seconds) to poll the API while the skybox is generating.")]
    [SerializeField] private float _pollInterval = 3f;

    [Tooltip("Maximum seconds to wait for generation before giving up.")]
    [SerializeField] private float _timeoutSeconds = 120f;

    [Header("Scene Skybox")]
    [Tooltip("The Material used as the scene skybox (Shader: Skybox/Panoramic).  " +
             "Drag in a material you create with the Skybox/Panoramic shader.")]
    [SerializeField] private Material _skyboxMaterial;

    [Tooltip("Fallback skybox material used while generation is in progress.")]
    [SerializeField] private Material _loadingSkyboxMaterial;

    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Fired when a new skybox texture is ready and applied.</summary>
    public event Action<Texture2D> OnSkyboxReady;

    // ── Constants ─────────────────────────────────────────────────────────────
    private const string API_BASE        = "https://backend.blockadelabs.com/api/v1";
    private const string ENDPOINT_CREATE = API_BASE + "/skybox";
    private const string ENDPOINT_STATUS = API_BASE + "/imagine/requests/{0}";

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a skybox from a text prompt and applies it to the current scene.
    /// Awaitable — returns the generated texture, or null on failure.
    /// </summary>
    public async UniTask<Texture2D> GenerateAndApplyAsync(string prompt)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            Debug.LogError("[SkyboxController] API key is empty.  Set it in the Inspector.");
            return null;
        }

        // Show loading skybox while we wait
        if (_loadingSkyboxMaterial != null)
            RenderSettings.skybox = _loadingSkyboxMaterial;

        string requestId = await CreateSkyboxRequestAsync(prompt);
        if (string.IsNullOrEmpty(requestId))
        {
            Debug.LogError("[SkyboxController] Failed to create skybox request.");
            return null;
        }

        Debug.Log($"[SkyboxController] Generation started. Request ID: {requestId}");

        Texture2D texture = await PollUntilReadyAsync(requestId);
        if (texture == null)
        {
            Debug.LogError("[SkyboxController] Skybox generation timed out or failed.");
            return null;
        }

        ApplyTexture(texture);
        OnSkyboxReady?.Invoke(texture);
        Debug.Log("[SkyboxController] Skybox applied successfully.");
        return texture;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async UniTask<string> CreateSkyboxRequestAsync(string prompt)
    {
        string json = _skyboxStyleId > 0
            ? $"{{\"prompt\":\"{EscapeJson(prompt)}\",\"skybox_style_id\":{_skyboxStyleId}}}"
            : $"{{\"prompt\":\"{EscapeJson(prompt)}\"}}";

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using var req = new UnityWebRequest(ENDPOINT_CREATE, "POST");
        req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("x-api-key", _apiKey);

        await req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[SkyboxController] Create request failed: {req.error}\n{req.downloadHandler.text}");
            return null;
        }

        var resp = JsonUtility.FromJson<SkyboxCreateResponse>(req.downloadHandler.text);
        return resp == null ? null : resp.id.ToString();
    }

    private async UniTask<Texture2D> PollUntilReadyAsync(string requestId)
    {
        float elapsed = 0f;
        string statusUrl = string.Format(ENDPOINT_STATUS, requestId);

        while (elapsed < _timeoutSeconds)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_pollInterval));
            elapsed += _pollInterval;

            using var req = UnityWebRequest.Get(statusUrl);
            req.SetRequestHeader("x-api-key", _apiKey);
            await req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success) continue;

            var status = JsonUtility.FromJson<SkyboxStatusResponse>(req.downloadHandler.text);

            if (status?.status == "complete" && !string.IsNullOrEmpty(status.file_url))
                return await DownloadTextureAsync(status.file_url);

            if (status?.status == "error")
            {
                Debug.LogError($"[SkyboxController] Generation error: {status.error_message}");
                return null;
            }

            Debug.Log($"[SkyboxController] Status: {status?.status ?? "unknown"} ({elapsed:F0}s elapsed)");
        }

        return null; // timed out
    }

    private static async UniTask<Texture2D> DownloadTextureAsync(string url)
    {
        using var req = UnityWebRequestTexture.GetTexture(url);
        await req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[SkyboxController] Texture download failed: {req.error}");
            return null;
        }

        return DownloadHandlerTexture.GetContent(req);
    }

    private void ApplyTexture(Texture2D texture)
    {
        if (_skyboxMaterial == null)
        {
            // Auto-create a Panoramic skybox material if none is assigned
            _skyboxMaterial = new Material(Shader.Find("Skybox/Panoramic"));
        }

        _skyboxMaterial.SetTexture("_MainTex", texture);
        RenderSettings.skybox = _skyboxMaterial;
        DynamicGI.UpdateEnvironment();
    }

    private static string EscapeJson(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

    // ── JSON response models ──────────────────────────────────────────────────

    [Serializable] private class SkyboxCreateResponse { public int id; }

    [Serializable]
    private class SkyboxStatusResponse
    {
        public string status;        // "pending" | "dispatched" | "processing" | "complete" | "error"
        public string file_url;      // URL of the equirectangular PNG
        public string error_message;
    }
}
