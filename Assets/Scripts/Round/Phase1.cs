using Unity.Netcode;
using UnityEngine;

public class Phase1 : APhase
{
    [SerializeField] private GameObject _TrapChestPrefab;

    private TrapSelection _trapSelection;

    private void Awake()
    {
        // Runs the chest sequence; added at runtime so the scene needs no extra setup.
        if (!TryGetComponent(out _trapSelection))
            _trapSelection = gameObject.AddComponent<TrapSelection>();
    }

    public override void Begin()
    {
        SwitchTileMapStateClientRpc(false);
        SetPlayersPlaying(false);
        TrapPlacementManager.Instance.DeactivateTraps();

        _trapSelection.Begin(_TrapChestPrefab);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
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
}