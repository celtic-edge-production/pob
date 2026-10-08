using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerManager : NetworkSingleton<PlayerManager>
{
    private bool _isDisconnecting = false;

    // Shown by the main menu after a session ended without the player asking (host gone).
    public static string PendingMenuMessage { get; set; }

    protected override void OnSingletonAwake()
    {
    }

    private void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        NetworkManager.Singleton.OnServerStopped += OnServerStopped;

        DontDestroyOnLoad(gameObject);
    }

    private void OnApplicationQuit()
    {
        // Quitting destroys every object in any order. Without a shutdown first, NGO sees a client
        // destroying its spawned objects as an invalid Destroy, and crashes while logging it
        // because the NetworkManager is already gone (NetworkLog.Header NullReferenceException).
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown(true);
    }

    private void OnClientConnected(ulong clientId)
    {
        if (IsServer)
            SendAllPlayersDataToNewClient(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.ShutdownInProgress)
            return;

        // On a client, NGO reports its own id when the connection to the host is lost
        // (host quit, crash or network drop). NetworkManager is checked rather than this
        // behaviour's IsServer, which is false once the PlayerManager is despawned.
        if (!NetworkManager.Singleton.IsServer)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId)
                OnConnectionToHostLost();
            return;
        }

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            Disconnect();
            return;
        }

        // NGO has already despawned the leaving player's objects (see PlayerIdentity.OnNetworkDespawn).
        // Only the game rules are left to update. Both managers only exist in a game scene.
        if (GameRoundManager.Instance != null)
        {
            GameRoundManager.Instance.HandlePlayerLeft(clientId);

            if (GameRoundManager.Instance.IsGameOver)
                return;
        }

        if (TrapPlacementManager.Instance != null)
            TrapPlacementManager.Instance.HandlePlayerLeft(clientId);
    }

    public void Disconnect()
    {
        StartDisconnect(connectionLost: false);
    }

    private void OnConnectionToHostLost()
    {
        PendingMenuMessage = "L'hôte a quitté la partie.";
        StartDisconnect(connectionLost: true);
    }

    private void StartDisconnect(bool connectionLost)
    {
        if (_isDisconnecting)
            return;

        _isDisconnecting = true;
        StartCoroutine(DisconnectRoutine(connectionLost));
    }

    private IEnumerator DisconnectRoutine(bool connectionLost)
    {
        // Close the matchmaking lobby too, or its code stays joinable while nobody hosts it.
        SteamLobbySession.Leave();
        EOSLobbySession.Leave();

        // Connection lost: NGO shuts down by itself right after the disconnect callback, discarding
        // the outgoing messages. Shutting down here first would try to send them to a dead host.
        if (!connectionLost && NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient))
        {
            NetworkManager.Singleton.Shutdown();
        }

        yield return null;

        if (NetworkManager.Singleton != null)
            Destroy(NetworkManager.Singleton.gameObject);

        foreach (PlayerCharacter character in FindObjectsByType<PlayerCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Destroy(character.gameObject);

        foreach (PlayerIdentity identity in FindObjectsByType<PlayerIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Destroy(identity.gameObject);

        if (MenuController.Instance != null)
            Destroy(MenuController.Instance.gameObject);

        SceneManager.LoadScene("Main Menu");

        Destroy(gameObject);
    }

    private void OnServerStopped(bool stop)
    {
        NetworkManager.Singleton.OnServerStopped -= OnServerStopped;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void SendAllPlayersDataToNewClient(ulong newClientId)
    {
        PlayerIdentity[] players = FindObjectsByType<PlayerIdentity>(FindObjectsSortMode.None);

        foreach (var player in players)
        {
            if (player.Data == null || player.Cursor == null)
                continue;
            player.SendDataToNewClient(newClientId);
        }
    }
}