using TMPro;
using UnityEngine;

public class LobbyMatchmaking : MonoBehaviour
{
    [SerializeField] private SceneManagerNetwork m_SceneManagerNetwork;
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
                m_SceneManagerNetwork.LoadSceneRpc("Lobby");
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