using Netcode.Transports;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

// Wraps the exact Steam lobby logic that used to live directly inside
// CreateJoinRoomManager (now LobbyMatchmaking). Behavior is unchanged from what we
// already tested together - only moved behind INetworkBackend.
public class SteamNetworkBackend : MonoBehaviour, INetworkBackend
{
    [SerializeField] private SteamNetworkingSocketsTransport _transport;
    [SerializeField] private int _maxPlayers = 4;

    private const string RoomCodeDataKey = "RoomCode";

    private System.Action<string> _onCodeReady;
    private System.Action<string> _onHostError;
    private System.Action<string> _onJoinError;
    private string _pendingJoinCode;
    private bool _isHosting;

    private Callback<LobbyCreated_t> _lobbyCreatedCallback;
    private Callback<LobbyMatchList_t> _lobbyMatchListCallback;
    private Callback<LobbyEnter_t> _lobbyEnteredCallback;
    private Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequestedCallback;

    public bool IsReady => SteamManager.Initialized;

    // Replaces Awake(): callbacks are only registered when this backend is the selected
    // one (NetworkBackendSelector calls Activate()), so an EOS build never touches Steam.
    public void Activate()
    {
        EnsureCallbacks();
    }

    private void EnsureCallbacks()
    {
        if (_lobbyCreatedCallback != null)
            return;

        _lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        _lobbyMatchListCallback = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);
        _lobbyEnteredCallback = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        _gameLobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
    }

    private void OnDestroy()
    {
        // Callback<T> stays registered with Steamworks until disposed. Without this, each
        // return to Main Menu left the previous instance's callbacks alive.
        _lobbyCreatedCallback?.Dispose();
        _lobbyMatchListCallback?.Dispose();
        _lobbyEnteredCallback?.Dispose();
        _gameLobbyJoinRequestedCallback?.Dispose();
    }

    public void Host(System.Action<string> onCodeReady, System.Action<string> onError)
    {
        EnsureCallbacks();

        if (!IsReady)
        {
            onError?.Invoke("Steam n'est pas disponible.");
            return;
        }

        _onCodeReady = onCodeReady;
        _onHostError = onError;
        _isHosting = true;

        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, _maxPlayers);
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (!_isHosting)
            return;

        if (callback.m_eResult != EResult.k_EResultOK)
        {
            _onHostError?.Invoke("Échec de création du lobby : " + callback.m_eResult);
            _isHosting = false;
            return;
        }

        CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        string roomCode = RoomCodeGenerator.Generate();

        SteamMatchmaking.SetLobbyData(lobbyId, RoomCodeDataKey, roomCode);
        SteamMatchmaking.SetLobbyJoinable(lobbyId, true);

        SteamLobbySession.CurrentLobbyId = lobbyId;
        SteamLobbySession.RoomCode = roomCode;

        if (NetworkManager.Singleton.StartHost())
        {
            _onCodeReady?.Invoke(roomCode);
        }
        else
        {
            _onHostError?.Invoke("Échec du démarrage de l'hébergement.");
        }

        _isHosting = false;
    }

    public void Join(string code, System.Action<string> onError)
    {
        EnsureCallbacks();

        if (!IsReady)
        {
            onError?.Invoke("Steam n'est pas disponible.");
            return;
        }

        if (string.IsNullOrEmpty(code))
        {
            onError?.Invoke("Entrez un code de lobby !");
            return;
        }

        _onJoinError = onError;
        _pendingJoinCode = code.Trim().ToUpperInvariant();

        SteamMatchmaking.AddRequestLobbyListStringFilter(
            RoomCodeDataKey, _pendingJoinCode, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.RequestLobbyList();
    }

    private void OnLobbyMatchList(LobbyMatchList_t callback)
    {
        if (string.IsNullOrEmpty(_pendingJoinCode))
            return;

        if (callback.m_nLobbiesMatching == 0)
        {
            _onJoinError?.Invoke("Aucun lobby trouvé pour ce code.");
            _pendingJoinCode = null;
            return;
        }

        CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(0);
        SteamMatchmaking.JoinLobby(lobbyId);
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        _pendingJoinCode = null;

        CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        CSteamID hostId = SteamMatchmaking.GetLobbyOwner(lobbyId);

        SteamLobbySession.CurrentLobbyId = lobbyId;
        SteamLobbySession.RoomCode = SteamMatchmaking.GetLobbyData(lobbyId, RoomCodeDataKey);

        if (hostId == SteamUser.GetSteamID())
            return; // We are the host - StartHost() already happened in OnLobbyCreated.

        if (_transport == null)
        {
            _onJoinError?.Invoke("SteamNetworkingSocketsTransport non assigné.");
            return;
        }

        _transport.ConnectToSteamID = hostId.m_SteamID;
        NetworkManager.Singleton.StartClient();
    }

    public void Disconnect()
    {
        if (NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient))
        {
            NetworkManager.Singleton.Shutdown();
        }
    }
}