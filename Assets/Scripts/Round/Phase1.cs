using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Phase1 : APhase
{
    [SerializeField] private GameObject _TrapChestPrefab;

    private TrapChest _TrapChest;

    [Rpc(SendTo.Server)]
    public override void StartPhaseServerRpc()
    {
        //ResetCameraClientRpc();

        SwitchTileMapStateClientRpc(false);

        _TrapChest = Instantiate(_TrapChestPrefab).GetComponent<TrapChest>();
        _TrapChest.transform.position = Vector3.zero;

        NetworkObject networkObject = _TrapChest.GetComponent<NetworkObject>();
        if (networkObject != null)
            networkObject.Spawn();
        else
            Debug.LogError("TrapChest Prefab must have a NetworkObject component.");

        SetPlayersPlaying(false);
        StartCoroutine(OpenChest());
        TrapPlacementManager.Instance.UnActivateCollideStateTrapServerRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void ResetCameraClientRpc()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.gameObject.SetActive(true);
            MainCameraController mainCameraController = mainCamera.GetComponent<MainCameraController>();
            if (mainCameraController != null)
                mainCameraController.ResetCamera();
        }
    }

    private IEnumerator OpenChest()
    {
        yield return new WaitForSeconds(3);
        _TrapChest.OpenChest();
    }
}