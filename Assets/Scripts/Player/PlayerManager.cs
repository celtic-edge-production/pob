using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerManager : NetworkSingleton<PlayerManager>
{
    private bool _isDisconnecting = false;

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

    private void OnClientConnected(ulong clientId)
    {
        if (IsServer)
            SendAllPlayersDataToNewClient(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.ShutdownInProgress)
            return;

        if (!IsServer)
            return;

        if (clientId == NetworkManager.Singleton.LocalClientId)
            Disconnect();
    }

    public void Disconnect()
    {
        if (_isDisconnecting)
            return;

        _isDisconnecting = true;
        StartCoroutine(DisconnectRoutine());
    }

    private IEnumerator DisconnectRoutine()
    {
        if (NetworkManager.Singleton != null &&
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
        NetworkManager.Singleton.OnServerStopped            -= OnServerStopped;
        NetworkManager.Singleton.OnClientConnectedCallback  -= OnClientConnected;
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
