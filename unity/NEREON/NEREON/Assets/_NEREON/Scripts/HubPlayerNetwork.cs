using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// NetworkBehaviour that lives on every connected player's prefab (local + remote).
///
/// SYNCED DATA (visible to all clients)
/// ──────────────────────────────────────
///   AvatarId  — drives which 3D character model is shown
///   Level     — drives AvatarProgressionEffects cosmetics
///   Username  — shown in FloatingNameTag above the player's head
///
/// FLOW
/// ─────
/// • OnNetworkSpawn:
///     - If IsOwner → read from HomeSceneManager.CachedProfile/Stats, set NetworkVars
///     - If not owner → subscribe to NetworkVar changes, spawn/update remote visuals
/// • On each NetworkVar change → RemotePlayerVisuals refreshes the avatar
///
/// SCENE SETUP
/// ─────────────
/// Add to the Hub Player prefab alongside:
///   NetworkObject, NetworkTransform, RemotePlayerVisuals, BubbleChat
/// </summary>
[RequireComponent(typeof(RemotePlayerVisuals))]
[RequireComponent(typeof(BubbleChat))]
public class HubPlayerNetwork : NetworkBehaviour
{
    // ── Synced State ──────────────────────────────────────────────────────────

    public NetworkVariable<byte>              AvatarId = new(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public NetworkVariable<ushort>            Level = new(1,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public NetworkVariable<FixedString64Bytes> Username = new("Player",
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // ── References ────────────────────────────────────────────────────────────

    private RemotePlayerVisuals _visuals;
    private BubbleChat          _bubbleChat;

    // ── NGO Lifecycle ─────────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        _visuals    = GetComponent<RemotePlayerVisuals>();
        _bubbleChat = GetComponent<BubbleChat>();

        if (IsOwner)
        {
            // Push our on-chain identity to the network
            var profile = HomeSceneManager.CachedProfile;
            var stats   = HomeSceneManager.CachedStats;

            AvatarId.Value = profile?.AvatarId ?? 0;
            Level.Value    = (ushort)(stats?.Level ?? 1);
            Username.Value = new FixedString64Bytes(HomeSceneManager.CachedUsername ?? "Player");

            Debug.Log($"[HubPlayerNetwork] Local player synced: avatar={AvatarId.Value} lv={Level.Value} name={Username.Value}");
        }
        else
        {
            // Remote player: build their avatar from current values and subscribe to changes
            _visuals.Refresh(AvatarId.Value, Level.Value, Username.Value.ToString());

            AvatarId.OnValueChanged += (_, v) => _visuals.Refresh(v, Level.Value, Username.Value.ToString());
            Level.OnValueChanged    += (_, v) => _visuals.Refresh(AvatarId.Value, v, Username.Value.ToString());
            Username.OnValueChanged += (_, v) => _visuals.Refresh(AvatarId.Value, Level.Value, v.ToString());
        }
    }

    // ── Chat ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by ChatInputUI on the local player's HubPlayerNetwork.
    /// Sends the message to the server which broadcasts to all clients.
    /// </summary>
    public void SendChatMessage(string text)
    {
        if (!IsOwner) return;
        SendChat_ServerRpc(new FixedString512Bytes(text));
    }

    [ServerRpc]
    private void SendChat_ServerRpc(FixedString512Bytes msg)
    {
        ShowChat_ClientRpc(msg);
    }

    [ClientRpc]
    private void ShowChat_ClientRpc(FixedString512Bytes msg)
    {
        _bubbleChat.ShowMessage(msg.ToString());
    }
}
