using Unity.Netcode;
using Unity.Netcode.Components;
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

    // The player controlling this character, or null (free lobby character). Every peer.
    public PlayerIdentity Player => Players.FindByCharacter(this);

    private SpriteRenderer _spriteRenderer;
    private PlayerMovement _movement;
    private PlayerCombat _combat;
    private TransferManager _bomb;
    private Hideable _hideable;

    private InputReader _Input;

    #region Lifecycle

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _movement = GetComponent<PlayerMovement>();
        _combat = GetComponent<PlayerCombat>();
        _bomb = GetComponent<TransferManager>();
        _hideable = Hideable.On(this);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _facingRight.OnValueChanged += OnFacingChanged;

        IsPlaying.OnValueChanged += OnIsPlayingChanged;
        ApplyPersistence(IsPlaying.Value);
    }

    public override void OnNetworkDespawn()
    {
        _facingRight.OnValueChanged -= OnFacingChanged;
        IsPlaying.OnValueChanged -= OnIsPlayingChanged;

        // The InputReader is a shared asset that outlives this character: a forgotten
        // subscription would keep calling into it after a disconnect.
        UnbindInput();

        base.OnNetworkDespawn();
    }

    private void OnFacingChanged(bool previous, bool facingRight)
    {
        _spriteRenderer.flipX = facingRight ? needDefaultFlip : !needDefaultFlip;
    }

    private void OnIsPlayingChanged(bool previous, bool playing)
    {
        ApplyPersistence(playing);
    }

    // A picked character follows its player from scene to scene: on a scene load, NGO carries over
    // every spawned object that has no parent and DestroyWithScene = false, on every peer. The flag
    // is only sent when the object spawns, so every peer sets it from IsPlaying (also fired on the
    // server when it writes it). A free lobby character stays with the lobby.
    private void ApplyPersistence(bool playing)
    {
        NetworkObject.DestroyWithScene = !playing;
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

        // Runs on the owner, so its player is this machine's. Not resolved through Player: the
        // replicated character reference may arrive one tick after this RPC.
        PlayerIdentity player = Players.Local;
        if (player == null || player.Input == null)
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

    // Visible (phase 2 or lobby), alive (bomb), not knocked out, not stunned. Every peer hides and
    // shows the character at the same time, the rest is replicated: valid on every peer.
    public bool CanAct()
    {
        return _hideable.IsVisible && _bomb.IsAlive.Value && !_combat.IsDown;
    }

    // Every peer. Hidden during phase 1: no sprite, no collision, no physics, no input.
    public bool IsVisible => _hideable.IsVisible;

    public void SetVisible(bool visible)
    {
        _hideable.SetVisible(visible);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void ResetPositionClientRpc(Vector3 position)
    {
        if (!IsOwner)
            return;

        // A teleport, not a move: with a plain transform.position, the other peers interpolated the
        // character from its old spot to its spawn point, and that copy (kinematic) shoved any player
        // on its way. The shoved player's copy then shoved back on the other machine, and both ran on
        // forever until one of them jumped.
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
            body.linearVelocity = Vector2.zero;

        if (TryGetComponent(out NetworkTransform networkTransform) && networkTransform.CanCommitToTransform)
            networkTransform.Teleport(position, transform.rotation, transform.localScale);
        else
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