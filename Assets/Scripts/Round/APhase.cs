using Unity.Netcode;
using UnityEngine;

public class APhase : NetworkBehaviour
{
    [SerializeField] protected GameObject _SpawnTileMap;

    // Server only, called by PhaseManager when the phase starts.
    public virtual void Begin()
    {
    }

    // Server only. Phase 1 shows the cursors (trap placement), phase 2 the characters.
    protected void SetPlayersPlaying(bool playing)
    {
        foreach (PlayerIdentity player in Players.All)
            player.SetPlayingClientRpc(playing);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    protected void SwitchTileMapStateClientRpc(bool value)
    {
        _SpawnTileMap.SetActive(value);
    }
}