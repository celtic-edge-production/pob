using Unity.Netcode;
using UnityEngine;

// Server side of the joining rules (ConnectionRules): NGO's connection approval refuses a player
// when the lobby is full or a game is running, and sends him the reason. Every backend goes through
// it (Steam and EOS cap their lobbies at 4, the Local backend had no limit at all).
// Added at runtime by InstallOn, so the NetworkManager needs no setup. Host and clients must both
// install it: ConnectionApproval is part of NGO's config check.
[DisallowMultipleComponent]
public class ConnectionGate : MonoBehaviour
{
    [SerializeField] private int _maxPlayers = ConnectionRules.DefaultMaxPlayers;

    private NetworkManager _networkManager;

    // Before StartHost / StartClient.
    public static void InstallOn(NetworkManager networkManager)
    {
        if (networkManager == null)
            return;

        if (!networkManager.TryGetComponent(out ConnectionGate gate))
            gate = networkManager.gameObject.AddComponent<ConnectionGate>();

        gate.Bind(networkManager);
    }

    private void Bind(NetworkManager networkManager)
    {
        _networkManager = networkManager;
        _networkManager.NetworkConfig.ConnectionApproval = true;
        _networkManager.ConnectionApprovalCallback = Approve;
    }

    private void OnDestroy()
    {
        if (_networkManager != null && _networkManager.ConnectionApprovalCallback == Approve)
            _networkManager.ConnectionApprovalCallback = null;
    }

    // Server only, called by NGO for every connection, the host's own included.
    private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // With approval on, NGO only spawns the player object when asked to.
        response.CreatePlayerObject = true;
        response.Pending = false;

        if (request.ClientNetworkId == NetworkManager.ServerClientId)
        {
            response.Approved = true;
            return;
        }

        // GameRoundManager only exists in a level scene.
        string reason = ConnectionRules.RefusalReason(
            _networkManager.ConnectedClientsIds.Count, _maxPlayers, GameRoundManager.Instance != null);

        response.Approved = reason == null;
        response.Reason = reason;
    }
}
