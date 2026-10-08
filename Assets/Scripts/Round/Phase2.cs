using Unity.Netcode;
using UnityEngine;
using TMPro;

// Phase 2: the characters are back and the round is played. The countdown and the trap
// activation are timed by PhaseManager.
public class Phase2 : APhase
{
    public TMP_Text timer;

    public override void Begin()
    {
        DesactivateCameraClientRpc();
        SwitchTileMapStateClientRpc(true);
        SetPlayersPlaying(true);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void DesactivateCameraClientRpc()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            mainCamera.gameObject.SetActive(false);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void ShowCountdownClientRpc(string text)
    {
        timer.text = text;
    }
}