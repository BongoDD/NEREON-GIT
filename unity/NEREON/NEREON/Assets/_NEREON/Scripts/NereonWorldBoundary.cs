using UnityEngine;

/// <summary>
/// NereonWorldBoundary — creates invisible boundary walls around the 200×200 hub world
/// and shows a "You can't go further" UI prompt when the player tries to leave.
///
/// The boundary matches the design doc: cliff walls North+West, river East, forest South.
/// Each edge has:
///   • An invisible BoxCollider (physics barrier)
///   • A trigger zone just inside it (shows the "boundary" prompt)
///
/// SCENE SETUP
/// ────────────
/// 1. Add to [Environment] GameObject in HomeScene. No additional wiring needed —
///    all colliders are created in Awake().
/// 2. Optionally wire _boundaryPrompt to a world-space or screen-space UI panel
///    with a TMP_Text child labelled "You can't go further."
///    If not wired, the component works silently.
/// 3. Set _worldSize to match WorldConfig SpawnPoint / terrain size (default 200).
/// </summary>
public class NereonWorldBoundary : MonoBehaviour
{
    [Header("World dimensions (must match terrain)")]
    [SerializeField] private float _worldSize    = 200f;
    [SerializeField] private float _wallHeight   = 40f;
    [SerializeField] private float _wallThickness = 4f;

    [Header("Prompt (optional)")]
    [SerializeField] private GameObject _boundaryPrompt;
    [SerializeField] private float      _promptDuration = 2.5f;

    private float _promptTimer;
    private bool  _promptVisible;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        BuildWalls();
    }

    private void Update()
    {
        if (!_promptVisible) return;
        _promptTimer -= Time.deltaTime;
        if (_promptTimer <= 0f) HidePrompt();
    }

    // ── Wall construction ─────────────────────────────────────────────────────

    private void BuildWalls()
    {
        float half = _worldSize * 0.5f;
        float midY = _wallHeight * 0.5f;

        // North wall
        CreateWall("BoundaryNorth",
            new Vector3(0, midY,  half + _wallThickness * 0.5f),
            new Vector3(_worldSize + _wallThickness * 2f, _wallHeight, _wallThickness));

        // South wall
        CreateWall("BoundarySouth",
            new Vector3(0, midY, -half - _wallThickness * 0.5f),
            new Vector3(_worldSize + _wallThickness * 2f, _wallHeight, _wallThickness));

        // East wall
        CreateWall("BoundaryEast",
            new Vector3( half + _wallThickness * 0.5f, midY, 0),
            new Vector3(_wallThickness, _wallHeight, _worldSize + _wallThickness * 2f));

        // West wall
        CreateWall("BoundaryWest",
            new Vector3(-half - _wallThickness * 0.5f, midY, 0),
            new Vector3(_wallThickness, _wallHeight, _worldSize + _wallThickness * 2f));

        // Trigger zones (1 unit inside each wall)
        float triggerInset = _wallThickness + 1f;
        float triggerDepth = 2f;

        CreateTrigger("TriggerNorth",  new Vector3(0, midY,  half - triggerInset),  new Vector3(_worldSize, _wallHeight, triggerDepth));
        CreateTrigger("TriggerSouth",  new Vector3(0, midY, -half + triggerInset),  new Vector3(_worldSize, _wallHeight, triggerDepth));
        CreateTrigger("TriggerEast",   new Vector3( half - triggerInset, midY, 0),  new Vector3(triggerDepth, _wallHeight, _worldSize));
        CreateTrigger("TriggerWest",   new Vector3(-half + triggerInset, midY, 0),  new Vector3(triggerDepth, _wallHeight, _worldSize));
    }

    private void CreateWall(string wallName, Vector3 localPos, Vector3 size)
    {
        var go  = new GameObject(wallName);
        go.transform.SetParent(transform);
        go.transform.localPosition = localPos;
        go.layer = LayerMask.NameToLayer("Default");

        var bc  = go.AddComponent<BoxCollider>();
        bc.size = size;
        bc.isTrigger = false;
    }

    private void CreateTrigger(string triggerName, Vector3 localPos, Vector3 size)
    {
        var go  = new GameObject(triggerName);
        go.transform.SetParent(transform);
        go.transform.localPosition = localPos;

        var bc  = go.AddComponent<BoxCollider>();
        bc.size = size;
        bc.isTrigger = true;

        var notifier = go.AddComponent<BoundaryTriggerNotifier>();
        notifier.Owner = this;
    }

    // ── Prompt ────────────────────────────────────────────────────────────────

    internal void OnBoundaryEntered()
    {
        if (_boundaryPrompt == null) return;
        _boundaryPrompt.SetActive(true);
        _promptVisible = true;
        _promptTimer   = _promptDuration;
    }

    private void HidePrompt()
    {
        _promptVisible = false;
        if (_boundaryPrompt != null) _boundaryPrompt.SetActive(false);
    }
}

/// <summary>Helper that relays OnTriggerEnter events back to NereonWorldBoundary.</summary>
public class BoundaryTriggerNotifier : MonoBehaviour
{
    internal NereonWorldBoundary Owner;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            Owner?.OnBoundaryEntered();
    }
}
