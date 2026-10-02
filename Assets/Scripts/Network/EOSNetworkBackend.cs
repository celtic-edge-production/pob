using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using PlayEveryWare.EpicOnlineServices;
using PlayEveryWare.EpicOnlineServices.Samples;
using PlayEveryWare.EpicOnlineServices.Samples.Network;
using UnityEngine;

// Alias: the plugin's Lobby class shares its name with the Epic.OnlineServices.Lobby namespace.
using SampleLobby = PlayEveryWare.EpicOnlineServices.Samples.Lobby;

// Short-code matchmaking on top of the EOS Lobby interface. The lobby is only a directory
// (code -> host ProductUserId); the actual connection is still EOSTransport P2P.
public class EOSNetworkBackend : MonoBehaviour, INetworkBackend
{
    private const string BucketId = "PanicOnBoard";
    private const string RoomCodeKey = "ROOMCODE"; // EOS uppercases attribute keys

    [SerializeField] private EOSTransport _transport;
    [SerializeField] private int _maxPlayers = 4;

    private bool _busy; // ignores double clicks while an async lobby operation is running

    public bool IsReady => EOSManager.Instance != null && EOSManager.Instance.GetProductUserId() != null;

    private static EOSLobbyManager Lobbies => EOSManager.Instance.GetOrCreateManager<EOSLobbyManager>();

    public void Host(System.Action<string> onCodeReady, System.Action<string> onError)
    {
        if (_busy)
            return;

        if (!IsReady)
        {
            onError?.Invoke("EOS n'est pas disponible.");
            return;
        }

        _busy = true;
        string code = RoomCodeGenerator.Generate();

        var lobby = new SampleLobby
        {
            MaxNumLobbyMembers = (uint)_maxPlayers,
            LobbyPermissionLevel = LobbyPermissionLevel.Publicadvertised,
            BucketId = BucketId,
            PresenceEnabled = false,      // presence needs an Epic account; we use anonymous Device ID
            RTCRoomEnabled = false,       // no voice chat
            DisableHostMigration = true,  // the lobby owner IS the NGO host: if they leave, the lobby should die
        };

        lobby.Attributes.Add(new LobbyAttribute
        {
            Key = RoomCodeKey,
            ValueType = AttributeType.String,
            AsString = code,
            Visibility = LobbyAttributeVisibility.Public,
        });

        Lobbies.CreateLobby(lobby, result =>
        {
            if (result != Result.Success)
            {
                _busy = false;
                onError?.Invoke($"Échec de création du lobby EOS : {result}");
                return;
            }

            if (!Unity.Netcode.NetworkManager.Singleton.StartHost())
            {
                Lobbies.LeaveLobby(null);
                _busy = false;
                onError?.Invoke("Échec du démarrage de l'hébergement.");
                return;
            }

            EOSLobbySession.CurrentLobbyId = Lobbies.GetCurrentLobby().Id;
            EOSLobbySession.RoomCode = code;

            _busy = false;
            onCodeReady?.Invoke(code);
        });
    }

    public void Join(string code, System.Action<string> onError)
    {
        if (_busy)
            return;

        if (!IsReady)
        {
            onError?.Invoke("EOS n'est pas disponible.");
            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            onError?.Invoke("Entrez le code du lobby !");
            return;
        }

        _busy = true;
        string normalizedCode = code.Trim().ToUpperInvariant();

        System.Action<string> fail = message =>
        {
            _busy = false;
            onError?.Invoke(message);
        };

        // JoinLobby refuses (InvalidState) while we are still in a previous lobby.
        LeaveCurrentLobbyThen(() =>
            Lobbies.SearchByAttribute(RoomCodeKey, normalizedCode,
                searchResult => OnSearchCompleted(normalizedCode, searchResult, fail)));
    }

    private void OnSearchCompleted(string code, Result searchResult, System.Action<string> fail)
    {
        if (searchResult == Result.NotFound)
        {
            fail("Aucun lobby trouvé pour ce code.");
            return;
        }

        if (searchResult != Result.Success)
        {
            fail($"Recherche impossible : {searchResult}");
            return;
        }

        SampleLobby found = null;
        LobbyDetails details = null;

        foreach (var entry in Lobbies.GetSearchResults())
        {
            found = entry.Key;
            details = entry.Value;
            break;
        }

        if (found == null)
        {
            fail("Aucun lobby trouvé pour ce code.");
            return;
        }

        Lobbies.JoinLobby(found.Id, details, false,
            joinResult => OnJoinCompleted(code, found, joinResult, fail));
    }

    private void OnJoinCompleted(string code, SampleLobby lobby, Result joinResult, System.Action<string> fail)
    {
        if (joinResult != Result.Success)
        {
            fail($"Impossible de rejoindre le lobby : {joinResult}");
            return;
        }

        ProductUserId hostId = lobby.LobbyOwner;

        if (hostId == null || !hostId.IsValid())
        {
            Lobbies.LeaveLobby(null);
            fail("Hôte introuvable.");
            return;
        }

        _transport.ServerUserIdToConnectTo = hostId;

        if (!Unity.Netcode.NetworkManager.Singleton.StartClient())
        {
            Lobbies.LeaveLobby(null);
            fail("Échec de la connexion.");
            return;
        }

        EOSLobbySession.CurrentLobbyId = lobby.Id;
        EOSLobbySession.RoomCode = code;
        _busy = false;
    }

    private void LeaveCurrentLobbyThen(System.Action next)
    {
        var current = Lobbies.GetCurrentLobby();

        if (current == null || !current.IsValid())
        {
            next();
            return;
        }

        Lobbies.LeaveLobby(_ => next());
    }

    public void Disconnect()
    {
        if (EOSManager.Instance != null)
            Lobbies.LeaveLobby(null);

        if (Unity.Netcode.NetworkManager.Singleton != null &&
            (Unity.Netcode.NetworkManager.Singleton.IsHost || Unity.Netcode.NetworkManager.Singleton.IsClient))
        {
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
        }
    }
}