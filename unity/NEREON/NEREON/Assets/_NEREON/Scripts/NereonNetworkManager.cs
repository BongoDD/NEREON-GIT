using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

/// <summary>
/// Manages the full Unity Gaming Services + NGO lifecycle for HomeScene.
///
/// NOTE: Uses com.unity.services.lobby + com.unity.services.relay (separate packages)
/// rather than com.unity.services.multiplayer, because the unified multiplayer package
/// bundles websocket-sharp-latest.dll which conflicts with the websocket-sharp.dll
/// embedded in the Solana Unity SDK. Migrate when the Solana SDK resolves its
/// WebSocket dependency.
///
/// FLOW
/// ─────
/// 1. Init Unity Services → sign in anonymously
/// 2. Query Lobby for an existing "NEREON_HUB" room
///    → none found: create lobby + Relay allocation → become Host
///    → found: join lobby → read relay code → become Client
/// 3. Configure UnityTransport with Relay data → start NGO Host or Client
///
/// SCENE SETUP
/// ─────────────
/// 1. Add to [Managers] GO in HomeScene.
/// 2. Wire _networkManager and _hubPlayerPrefab in Inspector.
/// 3. HomeSceneManager calls ConnectAsync(walletPubkey) after chain data loads.
/// </summary>
public class NereonNetworkManager : MonoBehaviour
{
    [Header("NGO")]
    [SerializeField] private NetworkManager _networkManager;
    [SerializeField] private GameObject     _hubPlayerPrefab;

    [Header("Lobby")]
    [SerializeField] private string _lobbyName  = "NEREON_HUB";
    [SerializeField] private int    _maxPlayers = 100;

    // ── State ─────────────────────────────────────────────────────────────────

    public static NereonNetworkManager Instance { get; private set; }
    public bool IsConnected => _networkManager != null && _networkManager.IsConnectedClient;

    private Lobby  _currentLobby;
    private bool   _isHost;
    private float  _heartbeatTimer;
    private const float HeartbeatInterval = 15f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (!_isHost || _currentLobby == null) return;
        _heartbeatTimer += Time.deltaTime;
        if (_heartbeatTimer >= HeartbeatInterval)
        {
            _heartbeatTimer = 0f;
            _ = LobbyService.Instance.SendHeartbeatPingAsync(_currentLobby.Id);
        }
    }

    private void OnDestroy()
    {
        if (_networkManager != null)
            _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        _ = LeaveLobbyAsync();
    }

    // ── Public ────────────────────────────────────────────────────────────────

    public async void ConnectAsync(string walletPubkey)
    {
        try
        {
            await InitServicesAsync();
            await SignInAsync();
            await JoinOrCreateLobbyAsync(walletPubkey ?? "anonymous");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NereonNetworkManager] Connection failed: {e.Message}");
        }
    }

    // ── UGS ───────────────────────────────────────────────────────────────────

    private async Task InitServicesAsync()
    {
        if (UnityServices.State == ServicesInitializationState.Initialized) return;
        await UnityServices.InitializeAsync();
    }

    private async Task SignInAsync()
    {
        if (AuthenticationService.Instance.IsSignedIn) return;
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        Debug.Log($"[NereonNetworkManager] Signed in: {AuthenticationService.Instance.PlayerId}");
    }

    // ── Lobby ─────────────────────────────────────────────────────────────────

    private async Task JoinOrCreateLobbyAsync(string walletPubkey)
    {
        try
        {
            var result = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions
            {
                Filters = new List<QueryFilter>
                {
                    new(QueryFilter.FieldOptions.Name,           _lobbyName, QueryFilter.OpOptions.EQ),
                    new(QueryFilter.FieldOptions.AvailableSlots, "0",        QueryFilter.OpOptions.GT)
                }
            });

            if (result.Results.Count > 0)
            {
                await JoinLobbyAsync(result.Results[0].Id, walletPubkey);
                return;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NereonNetworkManager] Lobby query failed, creating: {e.Message}");
        }

        await CreateLobbyAndHostAsync(walletPubkey);
    }

    private async Task CreateLobbyAndHostAsync(string walletPubkey)
    {
        _isHost = true;

        Allocation alloc     = await RelayService.Instance.CreateAllocationAsync(_maxPlayers - 1);
        string     relayCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

        _currentLobby = await LobbyService.Instance.CreateLobbyAsync(_lobbyName, _maxPlayers,
            new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    ["relayCode"] = new DataObject(DataObject.VisibilityOptions.Public, relayCode)
                },
                Player = MakePlayer(walletPubkey)
            });

        ConfigureHostTransport(alloc);
        StartNGO(host: true);
        Debug.Log($"[NereonNetworkManager] Lobby created, hosting. RelayCode={relayCode}");
    }

    private async Task JoinLobbyAsync(string lobbyId, string walletPubkey)
    {
        _isHost = false;

        _currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId,
            new JoinLobbyByIdOptions { Player = MakePlayer(walletPubkey) });

        string         relayCode = _currentLobby.Data["relayCode"].Value;
        JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(relayCode);

        ConfigureClientTransport(joinAlloc);
        StartNGO(host: false);
        Debug.Log("[NereonNetworkManager] Joined lobby as client.");
    }

    private static Player MakePlayer(string walletPubkey) => new(
        data: new Dictionary<string, PlayerDataObject>
        {
            ["wallet"] = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, walletPubkey)
        });

    // ── Transport ─────────────────────────────────────────────────────────────

    private void ConfigureHostTransport(Allocation a)
    {
        _networkManager.GetComponent<UnityTransport>().SetRelayServerData(
            a.RelayServer.IpV4, (ushort)a.RelayServer.Port,
            a.AllocationIdBytes, a.Key, a.ConnectionData);
    }

    private void ConfigureClientTransport(JoinAllocation a)
    {
        _networkManager.GetComponent<UnityTransport>().SetRelayServerData(
            a.RelayServer.IpV4, (ushort)a.RelayServer.Port,
            a.AllocationIdBytes, a.Key, a.ConnectionData, a.HostConnectionData);
    }

    private void StartNGO(bool host)
    {
        _networkManager.NetworkConfig.PlayerPrefab = _hubPlayerPrefab;
        _networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        if (host) _networkManager.StartHost();
        else      _networkManager.StartClient();
    }

    private void OnClientDisconnected(ulong id) =>
        Debug.Log($"[NereonNetworkManager] Client {id} disconnected.");

    private async Task LeaveLobbyAsync()
    {
        if (_currentLobby == null) return;
        try
        {
            if (_isHost) await LobbyService.Instance.DeleteLobbyAsync(_currentLobby.Id);
            else         await LobbyService.Instance.RemovePlayerAsync(
                             _currentLobby.Id, AuthenticationService.Instance.PlayerId);
        }
        catch { /* ignore on shutdown */ }
    }
}
