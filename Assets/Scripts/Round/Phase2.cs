using System.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class Phase2 : APhase
{
    [SerializeField] private PhaseManager _PhaseManager;

    public TMP_Text timer;

    void Start()
    {
    }

    protected override void Update()
    {

    }

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
        {
            Debug.Log("Camera name: " + mainCamera.name);
            mainCamera.gameObject.SetActive(false);
        }
    }

    public IEnumerator StartGameRound()
    {
        int countdown = 5;
        while (countdown > 0)
        {
            UpdateTimerClientRpc(countdown.ToString());
            yield return new WaitForSeconds(1);
            countdown--;
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
        GameRoundManager.Instance.ResetPlayers();
        TrapPlacementManager.Instance.ActivateCollideStateTrapServerRpc();
    }
}