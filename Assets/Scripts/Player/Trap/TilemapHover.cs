using Unity.Netcode;
using UnityEngine;

public class TilemapHover : NetworkBehaviour
{
    // TODO: create this class when player spawn and assign it to the player

    public Vector3 _worldPosition;
    private Vector3 _screenPosition;

    private ulong _playerId;

    public NetworkVariable<bool> isSettingTrap = new NetworkVariable<bool>();
    public NetworkVariable<bool> canPlaceTrap = new NetworkVariable<bool>();

    private InputReader _Input;

    public void OnEnable()
    {
        if (!IsOwner) return;

        _playerId = NetworkManager.Singleton.LocalClientId;
    }

    public void OnDisable()
    {
        // An RPC cannot be sent once despawned (scene change, disconnect).
        if (!IsOwner || !IsSpawned)
            return;

        ResetTilemapHoverServerRpc();
    }

    public void Init()
    {
        if (IsOwner) {
            if (transform.parent && transform.parent.TryGetComponent(out PlayerIdentity player)) {
                _Input = player.Input;

                _Input.RotateLeftEvent   += OnRotateLeft;
                _Input.RotateRightEvent  += OnRotateRight;
                _Input.MouseAction1Event += OnMouseAction1;
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void ResetTilemapHoverServerRpc()
    {
        // Sender check is native: [Rpc(SendTo.Server)] only accepts the owner.
        isSettingTrap.Value = false;
        canPlaceTrap.Value = false;
    }

    void Update()
    {
        if (!IsOwner || TrapPlacementManager.Instance == null) return;

        GetMousePosition();

        _playerId = NetworkManager.Singleton.LocalClientId;

        TrapPlacementManager.Instance.CheckMouseOnTileServerRpc(_worldPosition, _playerId);
    }

    private void OnMouseAction1()
    {
        if (canPlaceTrap.Value && isSettingTrap.Value)
            TrapPlacementManager.Instance.OnPlayerPlaceTrapServerRpc(_playerId, NetworkObjectId);
    }

    private void OnRotateLeft()
    {
        if (canPlaceTrap.Value)
            TrapPlacementManager.Instance.OnPlayerRotateTrapServerRpc(1, _playerId);
    }

    private void OnRotateRight()
    {
        if (canPlaceTrap.Value)
            TrapPlacementManager.Instance.OnPlayerRotateTrapServerRpc(-1, _playerId);
    }

    private void GetMousePosition()
    {
        _worldPosition = TrapUtils.GetMousePosition();
        transform.position = _worldPosition;
    }
}
