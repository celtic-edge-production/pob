using Netcode.Transports;
using PlayEveryWare.EpicOnlineServices.Samples.Network;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class NetworkBackendSelector : MonoBehaviour
{
    public enum Override { Auto, Steam, EOS, Local }

    [Header("Editor testing only (ignored in builds)")]
    [SerializeField] private Override _editorOverride = Override.Auto;

    [Header("SDK root objects - must be ROOT objects, unchecked (inactive) in the scene")]
    [SerializeField] private GameObject _steamServices;
    [SerializeField] private GameObject _eosServices;

    [Header("Backends")]
    [SerializeField] private SteamNetworkBackend _steamBackend;
    [SerializeField] private EOSNetworkBackend _eosBackend;
    [SerializeField] private LocalNetworkBackend _localBackend;

    [Header("NetworkManager + its three transports")]
    [SerializeField] private NetworkManager _networkManager;
    [SerializeField] private SteamNetworkingSocketsTransport _steamTransport;
    [SerializeField] private EOSTransport _eosTransport;
    [SerializeField] private UnityTransport _localTransport;

    public INetworkBackend Backend { get; private set; }
    public BackendType Selected { get; private set; }

    // Static so other scripts (e.g. LobbyManager) can know the active backend without a reference.
    public static BackendType Current { get; private set; }

    private void Awake()
    {
        Selected = ResolveBackendType();
        Current = Selected;

        // Only the chosen SDK root is activated, so the other SDK never initializes.
        if (_steamServices != null) _steamServices.SetActive(Selected == BackendType.Steam);
        if (_eosServices != null) _eosServices.SetActive(Selected == BackendType.EOS);

        NetworkTransport transport;

        switch (Selected)
        {
            case BackendType.Steam:
                transport = _steamTransport;
                _steamBackend.Activate();
                Backend = _steamBackend;
                break;
            case BackendType.EOS:
                transport = _eosTransport;
                Backend = _eosBackend;
                break;
            default:
                transport = _localTransport;
                Backend = _localBackend;
                break;
        }

        _networkManager.NetworkConfig.NetworkTransport = transport;

        // Disable the unused transports so their OnEnable/Update never runs in the wrong mode.
        if (_steamTransport != null) _steamTransport.enabled = Selected == BackendType.Steam;
        if (_eosTransport != null) _eosTransport.enabled = Selected == BackendType.EOS;
        if (_localTransport != null) _localTransport.enabled = Selected == BackendType.Local;

        Debug.Log($"[NetworkBackendSelector] Using {Selected}");
    }

    private BackendType ResolveBackendType()
    {
#if UNITY_EDITOR
        if (_editorOverride == Override.Steam) return BackendType.Steam;
        if (_editorOverride == Override.EOS) return BackendType.EOS;
        if (_editorOverride == Override.Local) return BackendType.Local;
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Development Builds: launch with "-localnet" to use the local backend.
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-localnet") >= 0)
            return BackendType.Local;
#endif
        return BuildBackend.Default;
    }
}