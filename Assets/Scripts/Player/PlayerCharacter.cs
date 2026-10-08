using Unity.Netcode;
using UnityEngine;

// A playable character: who controls it, where it faces, and its camera. The rest lives in
// PlayerMovement (running, jumping), PlayerCombat (punch, knock-out, stun) and TransferManager
// (bomb: holding, throwing, passing, alive). This component routes the owner's input to them.
[RequireComponent(typeof(PlayerMovement), typeof(PlayerCombat), typeof(TransferManager))]
public class PlayerCharacter : NetworkBehaviour
{
    // True while a player controls this character.
    public NetworkVariable<bool> IsPlaying = new(false);

    public Camera _playerCamera;
    public bool needDefaultFlip = false;

    // Owner-written: replicated only when it changes, and the sprite flip follows on every peer.
    private readonly NetworkVariable<bool> _facingRight = new(true,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public bool isFacingRight => _facingRight.Value;

    private SpriteRenderer _spriteRenderer;
    private PlayerMovement _movement;
    private PlayerCombat _combat;
    private TransferManager _bomb;

    private InputReader _Input;

    #region Lifecycle

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _movement = GetComponent<PlayerMovement>();
        _combat = GetComponent<PlayerCombat>();
        _bomb = GetComponent<TransferManager>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _facingRight.OnValueChanged += OnFacingChanged;
    }

    public override void OnNetworkDespawn()
    {
        _facingRight.OnValueChanged -= OnFacingChanged;

        // The InputReader is a shared asset that outlives this character: a forgotten
        // subscription would keep calling into it after a disconnect.
        UnbindInput();

        base.OnNetworkDespawn();
    }

    private void OnFacingChanged(bool previous, bool facingRight)
    {
        _spriteRenderer.flipX = facingRight ? needDefaultFlip : !needDefaultFlip;
    }

    // Owner only (PlayerMovement).
    public void SetFacing(bool facingRight)
    {
        if (IsOwner && _facingRight.Value != facingRight)
            _facingRight.Value = facingRight;
    }

    #endregion

    #region Ownership and input (server assigns, owner binds)

    // Server only: hands the character to a player (lobby selection).
    public void AssignToPlayer(ulong clientId)
    {
        NetworkObject.ChangeOwnership(clientId);
        IsPlaying.Value = true;

        // Resolved now, so it reaches the new owner (also works for the host, whose
        // ownership does not change since it already owned the lobby characters).
        BindInputOwnerRpc();
    }

    // Server only: gives the character back (right click in the lobby, player leaving).
    public void ReleaseFromPlayer()
    {
        IsPlaying.Value = false;

        // Still the leaving player's character here; skip it if he is already disconnected.
        if (NetworkManager.ConnectedClients.ContainsKey(OwnerClientId))
            UnbindInputOwnerRpc();

        NetworkObject.RemoveOwnership();
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void BindInputOwnerRpc()
    {
        // Never subscribe twice (double jump, double punch).
        UnbindInput();

        if (transform.parent == null || !transform.parent.TryGetComponent(out PlayerIdentity player))
            return;

        _Input = player.Input;
        _Input.MoveEvent += OnMove;
        _Input.JumpEvent += OnJumpReleased;
        _Input.JumpStartEvent += OnJumpPressed;
        _Input.AttackEvent += _combat.Punch;
        _Input.ThrowEventStart += OnThrowPressed;
        _Input.ThrowEventEnd += _bomb.OnThrowEnd;
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void UnbindInputOwnerRpc()
    {
        UnbindInput();
    }

    private void UnbindInput()
    {
        if (_Input == null)
            return;

        _Input.MoveEvent -= OnMove;
        _Input.JumpEvent -= OnJumpReleased;
        _Input.JumpStartEvent -= OnJumpPressed;
        _Input.AttackEvent -= _combat.Punch;
        _Input.ThrowEventStart -= OnThrowPressed;
        _Input.ThrowEventEnd -= _bomb.OnThrowEnd;
        _Input = null;
    }

    // Knocked out or stunned: no running, jumping or throwing (the punch checks it itself).
    private void OnMove(Vector2 movement)
    {
        if (IsOwner && CanAct())
            _movement.OnMove(movement);
    }

    private void OnJumpPressed()
    {
        if (IsOwner && CanAct())
            _movement.OnJumpInput();
    }

    private void OnJumpReleased()
    {
        if (IsOwner)
            _movement.OnJumpUpInput();
    }

    private void OnThrowPressed()
    {
        if (IsOwner && CanAct())
            _bomb.OnThrowStart();
    }

    #endregion

    #region State

    // Alive (bomb), not knocked out, not stunned. Uses replicated state: valid on every peer.
    public bool CanAct()
    {
        return _bomb.IsAlive.Value
            && !_combat.IsStunned.Value && !_combat.IsKnockedOut.Value;
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void ResetPositionClientRpc(Vector3 position)
    {
        if (!IsOwner)
            return;

        transform.position = position;
    }

    #endregion

    #region Camera

    public void ActivateCamera()
    {
        if (IsOwner)
            _playerCamera.gameObject.SetActive(true);
    }

    public void DesactivateCamera()
    {
        if (IsOwner && Camera.main != null)
            _playerCamera.gameObject.SetActive(false);
    }

    #endregion
}