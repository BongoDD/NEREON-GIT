using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Avatar selection carousel for WelcomeInitScene.
///
/// BEHAVIOUR
/// ──────────
/// • Left / Right buttons cycle through every entry in AvatarRegistry.
/// • When no prefab is assigned in the registry, a coloured capsule placeholder
///   is generated automatically via PlaceholderAvatarFactory.
/// • The selected avatar rotates slowly on a "preview stage" (a secondary camera
///   renders it into a RawImage so it floats inside the UI).
/// • Raises OnAvatarSelected when the player's choice changes — WelcomeSceneFlow
///   listens to this to keep WelcomeInitController.AvatarId in sync.
///
/// SCENE SETUP (Editor)
/// ─────────────────────
/// 1. Create an empty GO "PreviewStage" far from the main camera (e.g. x=500).
///    Add a secondary Camera child "PreviewCamera":
///       - Clear flags: Solid Colour, black background
///       - Culling mask: only the "AvatarPreview" layer
///       - Target Texture: a 512×512 RenderTexture asset
/// 2. Assign the "AvatarPreview" layer to every primitive PlaceholderAvatarFactory creates
///    (or to your real character prefabs) — see note in code below.
/// 3. In the UI: a RawImage whose Texture is the same RenderTexture asset.
/// 4. Wire all Inspector fields below.
/// </summary>
public class AvatarSelector : MonoBehaviour
{
    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Fires whenever the player changes the selection. Arg = chosen avatar_id byte.</summary>
    public System.Action<byte> OnAvatarSelected;

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Data")]
    [Tooltip("AvatarRegistry asset. If null, PlaceholderAvatarFactory is used directly.")]
    [SerializeField] private AvatarRegistry _registry;

    [Header("3-D Preview")]
    [Tooltip("Parent Transform at the off-screen preview stage (e.g. position 500,0,0).")]
    [SerializeField] private Transform _previewStage;

    [Tooltip("Degrees per second the preview model rotates.")]
    [SerializeField] private float _rotationSpeed = 45f;

    [Header("UI — Navigation")]
    [SerializeField] private Button _leftButton;
    [SerializeField] private Button _rightButton;

    [Header("UI — Info")]
    [SerializeField] private TextMeshProUGUI _classNameLabel;   // e.g. "WARRIOR"
    [SerializeField] private TextMeshProUGUI _classDescLabel;   // description text
    [SerializeField] private TextMeshProUGUI _pageIndicator;    // "1 / 4"

    // ── State ─────────────────────────────────────────────────────────────────
    private int        _selectedIndex;
    private int        _totalAvatars;
    private GameObject _previewInstance;

    // ── Unity ─────────────────────────────────────────────────────────────────
    private void Start()
    {
        _totalAvatars = (_registry != null && _registry.Avatars.Length > 0)
            ? _registry.Avatars.Length
            : PlaceholderAvatarFactory.Classes.Length;

        _leftButton?.onClick.AddListener(SelectPrevious);
        _rightButton?.onClick.AddListener(SelectNext);

        ShowAvatar(0);
    }

    private void Update()
    {
        if (_previewInstance != null)
            _previewInstance.transform.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime);
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    public void SelectNext()
    {
        ShowAvatar((_selectedIndex + 1) % _totalAvatars);
        UIAnimations.PopInAsync(transform).Forget();
    }

    public void SelectPrevious()
    {
        ShowAvatar((_selectedIndex - 1 + _totalAvatars) % _totalAvatars);
        UIAnimations.PopInAsync(transform).Forget();
    }

    /// <summary>Returns the currently selected avatar_id byte.</summary>
    public byte SelectedAvatarId => (byte)_selectedIndex;

    // ── Internal ──────────────────────────────────────────────────────────────

    private void ShowAvatar(int index)
    {
        _selectedIndex = index;

        // Tear down old preview
        if (_previewInstance != null) Destroy(_previewInstance);

        bool useRegistry = _registry != null && _registry.Avatars.Length > index;

        if (useRegistry)
        {
            var entry = _registry.Avatars[index];
            SpawnPreview(entry.Prefab);

            if (_classNameLabel) _classNameLabel.text = entry.DisplayName;
            if (_classDescLabel) _classDescLabel.text = entry.Description;
        }
        else
        {
            // Placeholder path — no prefab needed
            var cls  = (PlaceholderAvatarFactory.Class)(index % PlaceholderAvatarFactory.Classes.Length);
            var info = PlaceholderAvatarFactory.GetInfo((byte)index);
            _previewInstance = PlaceholderAvatarFactory.Create(cls, _previewStage);

            if (_classNameLabel) _classNameLabel.text = info.DisplayName;
            if (_classDescLabel) _classDescLabel.text = info.Description;
        }

        if (_pageIndicator)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _totalAvatars; i++)
            {
                if (i > 0) sb.Append("  ");
                sb.Append(i == _selectedIndex ? "<color=#FFE066>●</color>" : "<color=#555577>○</color>");
            }
            _pageIndicator.text = sb.ToString();
        }

        OnAvatarSelected?.Invoke((byte)index);
    }

    private void SpawnPreview(GameObject prefab)
    {
        if (prefab == null) return;
        var pos = _previewStage != null ? _previewStage.position : new Vector3(500f, 0f, 0f);
        var rot = _previewStage != null ? _previewStage.rotation : Quaternion.identity;
        _previewInstance = Instantiate(prefab, pos, rot, _previewStage);

        // Disable physics / inputs on the preview clone
        foreach (var rb  in _previewInstance.GetComponentsInChildren<Rigidbody>())   rb.isKinematic = true;
        foreach (var col in _previewInstance.GetComponentsInChildren<Collider>())    col.enabled    = false;
        foreach (var inp in _previewInstance.GetComponentsInChildren<UnityEngine.InputSystem.PlayerInput>())
            inp.enabled = false;
    }
}
