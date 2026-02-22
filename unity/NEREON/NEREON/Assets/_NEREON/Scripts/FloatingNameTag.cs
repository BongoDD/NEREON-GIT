using TMPro;
using UnityEngine;

/// <summary>
/// Floating UI label above the player's head showing Username + Level.
/// Attach this as a world-space Canvas child inside the avatar prefab.
///
/// SETUP IN EDITOR
/// ─────────────────
/// 1. Inside the avatar prefab, create a child GameObject "NameTag".
/// 2. Add a Canvas (World Space, scale ~0.01) and a CanvasScaler.
/// 3. Inside the Canvas, add two TMP labels: one for the username, one for "LVL X".
/// 4. Add this component to the NameTag GameObject.
/// 5. Wire _usernameLabel, _levelLabel in Inspector.
/// 6. Drag the FloatingNameTag prefab reference into AvatarManager._nameTagPrefab.
///
/// The label always faces the main camera (billboard behaviour).
/// </summary>
public class FloatingNameTag : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _usernameLabel;
    [SerializeField] private TextMeshProUGUI _levelLabel;

    [Tooltip("Vertical offset above the avatar's root position.")]
    [SerializeField] private float _heightOffset = 2.4f;

    private Transform _cam;

    private void Start()
    {
        var cam = Camera.main;
        if (cam != null) _cam = cam.transform;

        // Position above the avatar
        transform.localPosition = new Vector3(0f, _heightOffset, 0f);
    }

    private void LateUpdate()
    {
        // Billboard: always face the camera
        if (_cam != null)
            transform.rotation = Quaternion.LookRotation(
                transform.position - _cam.position);
    }

    /// <summary>Set content — called by AvatarManager after on-chain data is ready.</summary>
    public void Initialise(string username, ushort level)
    {
        if (_usernameLabel) _usernameLabel.text = username;
        if (_levelLabel)    _levelLabel.text    = $"LVL {level}";
    }

    /// <summary>Call after a level-up to refresh the label without re-spawning the avatar.</summary>
    public void SetLevel(ushort level)
    {
        if (_levelLabel) _levelLabel.text = $"LVL {level}";
    }
}
