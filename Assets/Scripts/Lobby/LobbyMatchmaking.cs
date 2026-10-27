using TMPro;
using Unity.Netcode;
using UnityEngine;

public class LobbyMatchmaking : MonoBehaviour
{
    [SerializeField] private TMP_InputField _LobbyCodeInputField;
    [SerializeField] private TMP_Text _MessageText;

    // Replaces the old manually-assigned _backendBehaviour field.
    [SerializeField] private NetworkBackendSelector _backendSelector;

    private INetworkBackend _backend;
    private string _currentHostCode;

    private void Awake()
    {
        _backend = _backendSelector != null ? _backendSelector.Backend : null;

        if (_backend == null)
            Debug.LogError("LobbyMatchmaking: no backend available from NetworkBackendSelector.");
    }

    private void Start()
    {
        // The previous session ended without the player asking (host gone): tell him why.
        if (!string.IsNullOrEmpty(PlayerManager.PendingMenuMessage) && _MessageText != null)
        {
            _MessageText.text = PlayerManager.PendingMenuMessage;
            PlayerManager.PendingMenuMessage = null;
        }

        // Before any host or join: the joining rules, and the reason a refused player is told.
        NetworkManager networkManager = NetworkManager.Singleton;
        ConnectionGate.InstallOn(networkManager);
        if (networkManager != null)
            networkManager.OnClientDisconnectCallback += OnDisconnectedFromMenu;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnectedFromMenu;
    }

    // Still in the menu: the join failed or was refused by the host (lobby full, game running).
    private void OnDisconnectedFromMenu(ulong clientId)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.IsServer || clientId != networkManager.LocalClientId)
            return;

        string reason = networkManager.DisconnectReason;
        if (_MessageText != null)
            _MessageText.text = string.IsNullOrEmpty(reason) ? "Impossible de rejoindre le lobby." : reason;

        // Leave the Steam / EOS lobby joined on the way. NGO shuts itself down right after this
        // callback: shutting it down here would try to talk to a host that already said no.
        SteamLobbySession.Leave();
        EOSLobbySession.Leave();
    }

    public void InitNetworkLobbyAndRelay()
    {
        if (_MessageText != null)
            _MessageText.text = "Création du lobby...";

        _backend.Host(
            onCodeReady: code =>
            {
                _currentHostCode = code;
                if (_MessageText != null)
                    _MessageText.text = "Code du lobby : " + code;
                SceneLoader.LoadForEveryone(SceneNames.Lobby);
            },
            onError: message =>
            {
                if (_MessageText != null)
                    _MessageText.text = message;
            });
    }

    public void JoinLobbyByCode()
    {
        string code = _LobbyCodeInputField != null ? _LobbyCodeInputField.text : null;

        if (_MessageText != null)
            _MessageText.text = "Recherche du lobby...";

        _backend.Join(code, message =>
        {
            if (_MessageText != null)
                _MessageText.text = message;
        });
    }

    public void CopyHostCodeToClipboard()
    {
        if (!string.IsNullOrEmpty(_currentHostCode))
            GUIUtility.systemCopyBuffer = _currentHostCode;
    }
}