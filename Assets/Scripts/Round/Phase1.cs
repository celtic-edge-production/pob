using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Phase1 : APhase
{
    [SerializeField] private GameObject _TrapChestPrefab;

    private TrapChest _TrapChest;

    protected override void Update()
    {

    }

    [Rpc(SendTo.Server)]
    public override void StartPhaseServerRpc()
    {
        //ResetCameraClientRpc();
        //Debug.Log("Phase 1 Started");

        SwitchTileMapStateClientRpc(false);

        // Spawn the TrapChest
        _TrapChest = Instantiate(_TrapChestPrefab).GetComponent<TrapChest>();
        _TrapChest.transform.position = new Vector3(0, 0, 0);

        // Make sure it has a NetworkObject component
        NetworkObject networkObject = _TrapChest.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.Spawn(); // This will spawn the object on all clients
        }
        else
        {
            Debug.LogError("TrapChest Prefab must have a NetworkObject component.");
        }

        SwitchStatePlayersServerRpc();
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
            {
                mainCameraController.ResetCamera();
            }
        }
    }

    public IEnumerator OpenChest()
    {
        //if (!IsHost) return;
        yield return new WaitForSeconds(3);

        //Debug.Log("OpenChest");
        _TrapChest.OpenChest();
    }
}
