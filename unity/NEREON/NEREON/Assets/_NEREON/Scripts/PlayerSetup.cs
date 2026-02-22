using UnityEngine;

/// <summary>
/// PlayerSetup — sits on the HubPlayer prefab root alongside StarterAssets ThirdPersonController.
///
/// Responsibilities:
///  1. Warps the player to SpawnPoint at scene start (before ThirdPersonController ticks).
///  2. Locks/hides the cursor so third-person camera works immediately.
///  3. Exposes static ReleaseCursor/LockCursor so ChatInputUI can toggle it.
///  4. Keeps a static singleton so other systems can find the local player easily.
///
/// COMPATIBLE WITH Starter Assets – Third Person:
///  • Does NOT control movement — StarterAssets ThirdPersonController does that.
///  • Only sets position once at Start(), then hands off completely.
///
/// SCENE SETUP
/// ────────────
/// 1. This component is added automatically by NereonPlayerPrefabBuilder.
/// 2. Wire _spawnPoint → [Districts/CentralPlaza/SpawnPoint] in Inspector.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerSetup : MonoBehaviour
{
    [Header("World")]
    [SerializeField] private Transform _spawnPoint;

    [Header("Camera")]
    [SerializeField] private Camera _playerCamera;   // optional; falls back to Camera.main

    // ── Singleton ─────────────────────────────────────────────────────────────

    public static PlayerSetup LocalPlayer { get; private set; }
    public static Transform   Transform   => LocalPlayer != null ? LocalPlayer.transform : null;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        LocalPlayer = this;
    }

    private void Start()
    {
        PlaceAtSpawnPoint();
        SetupCursor();
        WireNetworkIdentity();
    }

    private void OnDestroy()
    {
        if (LocalPlayer == this) LocalPlayer = null;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    // ── Spawn ─────────────────────────────────────────────────────────────────

    private void PlaceAtSpawnPoint()
    {
        if (_spawnPoint == null)
        {
            // Try to find it by tag as a fallback
            var spawnGO = GameObject.FindWithTag("SpawnPoint");
            if (spawnGO != null) _spawnPoint = spawnGO.transform;
        }

        if (_spawnPoint == null)
        {
            Debug.LogWarning("[PlayerSetup] No SpawnPoint found — player stays at origin.");
            return;
        }

        // Warp CharacterController safely (disable → move → enable)
        var cc = GetComponent<CharacterController>();
        cc.enabled = false;
        transform.SetPositionAndRotation(_spawnPoint.position, _spawnPoint.rotation);
        cc.enabled = true;

        Debug.Log($"[PlayerSetup] Spawned at {_spawnPoint.position}");
    }

    // ── Cursor ────────────────────────────────────────────────────────────────

    private void SetupCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    /// <summary>
    /// Call from anywhere to temporarily release the cursor (e.g., chat input open).
    /// </summary>
    public static void ReleaseCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    /// <summary>
    /// Call when chat/menu closes to re-lock the cursor.
    /// </summary>
    public static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // ── Network identity ──────────────────────────────────────────────────────

    private void WireNetworkIdentity()
    {
        var net = GetComponent<HubPlayerNetwork>();
        if (net == null) return;

        // HubPlayerNetwork picks up the cached values from HomeSceneManager
        // when it spawns; this method is a hook for any extra pre-spawn wiring.
        Debug.Log("[PlayerSetup] HubPlayerNetwork found and ready.");
    }

    // ── Camera ────────────────────────────────────────────────────────────────

    public Camera GetPlayerCamera() =>
        _playerCamera != null ? _playerCamera : Camera.main;
}
