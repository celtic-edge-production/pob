using Unity.Netcode;
using UnityEngine;

// Phase 1 cursor: follows the mouse, previews and places the player's trap.
public class TilemapHover : NetworkBehaviour
{
    public Vector3 _worldPosition;

    public NetworkVariable<bool> isSettingTrap = new NetworkVariable<bool>();
    public NetworkVariable<bool> canPlaceTrap = new NetworkVariable<bool>();

    private InputReader _Input;

    // Last cell sent to the server: the preview only needs an update when it changes.
    private Vector3Int _lastSentCell;
    private bool _hasSentCell;

    public void OnDisable()
    {
        // An RPC cannot be sent once despawned (scene change, disconnect).
        if (!IsOwner || !IsSpawned)
            return;

        ResetTilemapHoverServerRpc();
    }

    public void Init()
    {
        if (!IsOwner)
            return;

        if (transform.parent && transform.parent.TryGetComponent(out PlayerIdentity player))
        {
            _Input = player.Input;

            _Input.RotateLeftEvent += OnRotateLeft;
            _Input.RotateRightEvent += OnRotateRight;
            _Input.MouseAction1Event += OnMouseAction1;
        }
    }

    [Rpc(SendTo.Server)]
    private void ResetTilemapHoverServerRpc()
    {
        // Sender check is native: [Rpc(SendTo.Server)] only accepts the owner.
        isSettingTrap.Value = false;
        canPlaceTrap.Value = false;
    }

    private void Update()
    {
        if (!IsOwner || TrapPlacementManager.Instance == null)
            return;

        _worldPosition = TrapUtils.GetMousePosition();
        transform.position = _worldPosition;

        SendPreviewCellIfChanged();
    }

    private void SendPreviewCellIfChanged()
    {
        // The server ignores the position while this player has no trap to place.
        if (!isSettingTrap.Value)
        {
            _hasSentCell = false;
            return;
        }

        Vector3Int cell = TrapPlacementManager.Instance.WorldToCell(_worldPosition);
        if (_hasSentCell && cell == _lastSentCell)
            return;

        _lastSentCell = cell;
        _hasSentCell = true;
        TrapPlacementManager.Instance.CheckMouseOnTileServerRpc(_worldPosition, OwnerClientId);
    }

    private void OnMouseAction1()
    {
        if (canPlaceTrap.Value && isSettingTrap.Value)
            TrapPlacementManager.Instance.OnPlayerPlaceTrapServerRpc(OwnerClientId, NetworkObjectId);
    }

    private void OnRotateLeft() => Rotate(1);

    private void OnRotateRight() => Rotate(-1);

    private void Rotate(int direction)
    {
        if (canPlaceTrap.Value)
            TrapPlacementManager.Instance.OnPlayerRotateTrapServerRpc(direction, OwnerClientId);
    }
}