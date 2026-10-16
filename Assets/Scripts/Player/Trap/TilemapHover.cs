using Unity.Netcode;
using UnityEngine;

// Phase 1 cursor: picks a trap in the chest, then moves, turns and places it.
// The player owns the trap he holds (TrapPlacementManager gives it to him) and moves it locally:
// the preview is instant on his side, and the trap's ClientNetworkTransform shows it to the others.
// The server only answers whether the preview is valid, and checks everything again on placement.
public class TilemapHover : NetworkBehaviour
{
    public Vector3 _worldPosition;

    public NetworkVariable<bool> isSettingTrap = new NetworkVariable<bool>();
    public NetworkVariable<bool> canPlaceTrap = new NetworkVariable<bool>();

    private InputReader _Input;

    // Owner only: the trap being placed and how the player has turned it.
    private ATrap _heldTrap;
    private TrapOrientation _orientation = TrapOrientation.Default;
    private Vector3Int _cell;

    // Owner only: the last preview sent to the server, which only needs to hear about changes.
    private Vector3Int _lastSentCell;
    private TrapOrientation _lastSentOrientation;
    private bool _hasSentPreview;

    private Hideable _hideable;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // The cursor's visibility (CursorController): this component only acts while it is shown.
        _hideable = Hideable.On(this);
        _hideable.VisibilityChanged += OnVisibilityChanged;
    }

    // Hidden (phase 2, character picked): whatever trap was being set is dropped.
    private void OnVisibilityChanged(bool visible)
    {
        // An RPC cannot be sent once despawned (scene change, disconnect).
        if (visible || !IsOwner || !IsSpawned)
            return;

        ResetTilemapHoverServerRpc();
    }

    public override void OnNetworkDespawn()
    {
        if (_hideable != null)
            _hideable.VisibilityChanged -= OnVisibilityChanged;

        // The InputReader is a shared asset that outlives this cursor: release it, or the next
        // session's clicks still reach this one.
        if (_Input != null)
        {
            _Input.RotateLeftEvent -= OnRotateLeft;
            _Input.RotateRightEvent -= OnRotateRight;
            _Input.MirrorEvent -= OnMirror;
            _Input.MouseAction1Event -= OnMouseAction1;
            _Input = null;
        }

        base.OnNetworkDespawn();
    }

    public void Init()
    {
        if (!IsOwner || _Input != null)
            return;

        if (transform.parent && transform.parent.TryGetComponent(out PlayerIdentity player))
        {
            _Input = player.Input;

            _Input.RotateLeftEvent += OnRotateLeft;
            _Input.RotateRightEvent += OnRotateRight;
            _Input.MirrorEvent += OnMirror;
            _Input.MouseAction1Event += OnMouseAction1;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ResetTilemapHoverServerRpc()
    {
        // Only the owner may reset its own cursor state.
        isSettingTrap.Value = false;
        canPlaceTrap.Value = false;
    }

    private void Update()
    {
        if (!IsOwner || TrapPlacementManager.Instance == null || !IsShown)
            return;

        _worldPosition = TrapUtils.GetMousePosition();
        transform.position = _worldPosition;

        UpdateHeldTrap();
    }

    private void UpdateHeldTrap()
    {
        if (!isSettingTrap.Value)
        {
            _heldTrap = null;
            _hasSentPreview = false;
            return;
        }

        // Ownership arrives shortly after the choice: look for the trap until it is ours.
        if (_heldTrap == null || !_heldTrap.IsSpawned || !_heldTrap.IsOwner)
        {
            _heldTrap = FindHeldTrap();
            _orientation = TrapOrientation.Default;
            _hasSentPreview = false;

            if (_heldTrap == null)
                return;
        }

        _cell = TrapPlacementManager.Instance.WorldToCell(_worldPosition);
        _heldTrap.ShowAt(_cell, _orientation);

        if (_hasSentPreview && _cell == _lastSentCell && _orientation.Equals(_lastSentOrientation))
            return;

        _lastSentCell = _cell;
        _lastSentOrientation = _orientation;
        _hasSentPreview = true;
        TrapPlacementManager.Instance.OnPlayerPreviewChangedServerRpc(
            _cell.x, _cell.y, (byte)_orientation.QuarterTurns, _orientation.Mirrored, OwnerClientId);
    }

    private static ATrap FindHeldTrap()
    {
        foreach (ATrap trap in FindObjectsByType<ATrap>(FindObjectsSortMode.None))
        {
            if (trap.IsSpawned && trap.IsOwner && TrapStageRules.IsHeld(trap.Stage.Value))
                return trap;
        }
        return null;
    }

    // The input events keep coming while the cursor is hidden: they are ignored then.
    private bool IsShown => _hideable != null && _hideable.IsVisible;

    private void OnMouseAction1()
    {
        if (!IsShown)
            return;

        if (!isSettingTrap.Value)
        {
            TrySelectTrapUnderCursor();
            return;
        }

        // The server refuses it if the chest is still open or the cell is not valid.
        if (canPlaceTrap.Value && _heldTrap != null)
        {
            TrapPlacementManager.Instance.OnPlayerPlaceTrapServerRpc(
                _cell.x, _cell.y, (byte)_orientation.QuarterTurns, _orientation.Mirrored, OwnerClientId, NetworkObjectId);
        }
    }

    private void TrySelectTrapUnderCursor()
    {
        foreach (ATrap trap in FindObjectsByType<ATrap>(FindObjectsSortMode.None))
        {
            if (trap.IsSpawned && trap.Stage.Value == TrapStage.InChest && trap.IsUnderPoint(_worldPosition))
            {
                TrapPlacementManager.Instance.OnPlayerSelectedATrapServerRpc(trap.trapId.Value, OwnerClientId, NetworkObjectId);
                return;
            }
        }
    }

    // Applied on the next Update, together with the move: the trap turns in one step.
    private void OnRotateLeft() => Turn(_orientation.Rotated(1));

    private void OnRotateRight() => Turn(_orientation.Rotated(-1));

    private void OnMirror() => Turn(_orientation.FlippedHorizontally());

    private void Turn(TrapOrientation orientation)
    {
        if (IsShown && isSettingTrap.Value && _heldTrap != null)
            _orientation = orientation;
    }
}