using Unity.Netcode;
using UnityEngine;

public class APhase : NetworkBehaviour
{
    [SerializeField] protected GameObject _SpawnTileMap;

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Server)]
    virtual public void StartPhaseServerRpc()
    {

    }

    // Server only. Phase 1 shows the cursors (trap placement), phase 2 the characters.
    protected void SetPlayersPlaying(bool playing)
    {
        foreach (PlayerIdentity identity in FindObjectsByType<PlayerIdentity>(FindObjectsSortMode.None))
            identity.SetPlayingClientRpc(playing);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    protected void SwitchTileMapStateClientRpc(bool value)
    {
        _SpawnTileMap.SetActive(value);
    }
}