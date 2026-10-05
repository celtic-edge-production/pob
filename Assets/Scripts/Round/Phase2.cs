using System.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class Phase2 : APhase
{
    public TMP_Text timer;

    [Rpc(SendTo.Server)]
    public override void StartPhaseServerRpc()
    {
        DesactivateCameraClientRpc();
        SwitchTileMapStateClientRpc(true);
        SetPlayersPlaying(true);
        StartCoroutine(StartGameRound());
        StartCoroutine(ActivateTraps());
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void DesactivateCameraClientRpc()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            mainCamera.gameObject.SetActive(false);
    }

    private IEnumerator StartGameRound()
    {
        for (int countdown = 5; countdown > 0; countdown--)
        {
            UpdateTimerClientRpc(countdown.ToString());
            yield return new WaitForSeconds(1);
        }

        UpdateTimerClientRpc("Go!");
        yield return new WaitForSeconds(1);

        UpdateTimerClientRpc("");
        GameRoundManager.Instance.StartNewRound();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateTimerClientRpc(string text)
    {
        timer.text = text;
    }

    private IEnumerator ActivateTraps()
    {
        yield return new WaitForSeconds(2);

        // Revive everyone and rebuild the round's player list before the traps go live.
        GameRoundManager.Instance.ResetPlayers();
        TrapPlacementManager.Instance.ActivateCollideStateTrapServerRpc();
    }
}