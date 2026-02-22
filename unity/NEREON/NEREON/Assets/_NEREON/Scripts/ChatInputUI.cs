using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space chat input UI for the local player.
///
/// CONTROLS
/// ─────────
///   T           → open chat input (only when not already typing)
///   Enter       → send message and close
///   Escape      → cancel and close
///
/// SCENE SETUP (add to [HUD Canvas])
/// ─────────────
/// ChatInputPanel (CanvasGroup, anchored bottom-centre)
/// └── Background Image
/// └── InputField (TMP_InputField, char limit 120, placeholder "Say something…")
/// └── SendButton (Button, optional — Enter also sends)
///
/// Wire _chatPanel, _inputField, and _sendButton in the Inspector.
/// Wire _localPlayerNetwork in the Inspector, OR leave null and this script
/// will find it at runtime via NetworkManager.LocalClient.PlayerObject.
/// </summary>
public class ChatInputUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup   _chatPanel;
    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private Button        _sendButton;

    [Header("Optional")]
    [Tooltip("Leave empty — auto-found via NetworkManager at runtime.")]
    [SerializeField] private HubPlayerNetwork _localPlayerNetwork;

    [Tooltip("Key to open chat. Default: T")]
    [SerializeField] private KeyCode _openKey = KeyCode.T;

    [Tooltip("Maximum message length (characters).")]
    [SerializeField] private int _maxLength = 120;

    // ── State ─────────────────────────────────────────────────────────────────

    private bool _isOpen;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        SetPanelVisible(false);

        if (_inputField != null)
        {
            _inputField.characterLimit = _maxLength;
            _inputField.onSubmit.AddListener(OnSubmit);
        }

        if (_sendButton != null)
            _sendButton.onClick.AddListener(Send);
    }

    private void Update()
    {
        if (!_isOpen && Input.GetKeyDown(_openKey))
        {
            Open();
            return;
        }

        if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void Open()
    {
        _isOpen = true;
        SetPanelVisible(true);

        if (_inputField != null)
        {
            _inputField.text = string.Empty;
            _inputField.ActivateInputField();
            _inputField.Select();
        }
    }

    private void Close()
    {
        _isOpen = false;
        SetPanelVisible(false);
        if (_inputField != null) _inputField.DeactivateInputField();
    }

    private void OnSubmit(string text) => Send();

    private void Send()
    {
        if (_inputField == null) return;

        string msg = _inputField.text.Trim();
        if (string.IsNullOrEmpty(msg)) { Close(); return; }

        // Clamp just in case
        if (msg.Length > _maxLength) msg = msg[.._maxLength];

        GetLocalNetwork()?.SendChatMessage(msg);

        Close();
    }

    private HubPlayerNetwork GetLocalNetwork()
    {
        if (_localPlayerNetwork != null) return _localPlayerNetwork;

        // Auto-find: look for the NetworkObject owned by local client
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm == null || nm.LocalClient?.PlayerObject == null) return null;

        _localPlayerNetwork = nm.LocalClient.PlayerObject.GetComponent<HubPlayerNetwork>();
        return _localPlayerNetwork;
    }

    private void SetPanelVisible(bool visible)
    {
        if (_chatPanel == null) return;
        _chatPanel.alpha          = visible ? 1f : 0f;
        _chatPanel.interactable   = visible;
        _chatPanel.blocksRaycasts = visible;
    }
}
