using Unity.Netcode;
using UnityEngine;

public class PlayerCharacter : NetworkBehaviour
{
    [Header("Ownership")]
    // True while a player controls this character.
    public NetworkVariable<bool> IsPlaying = new(false);

    [Header("Player Settings")]
    public float punchForce = 5;
    public float punchRadius = 1.0f;
    public Camera _playerCamera;
    public bool needDefaultFlip = false;
    private Rigidbody2D rigidBody2D;
    private SpriteRenderer spriteRenderer;
    private bool isGrounded;
    private Animator animator;

    public bool IsJumping { get; private set; }
    public bool IsWallJumping { get; private set; }
    public bool IsSliding { get; private set; }

    public float LastOnGroundTime { get; private set; }
    public float LastOnWallTime { get; private set; }
    public float LastOnWallRightTime { get; private set; }
    public float LastOnWallLeftTime { get; private set; }

    public NetworkVariable<bool> isStun = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isStunHit = new NetworkVariable<bool>(false);

    public GameObject stunIndicator;
    private float currentStunTime = 0f;

    private TransferManager _transferManager;

    // Jump
    private bool _isJumpCut;
    private bool _isJumpFalling;

    // Wall jump
    private float _wallJumpStartTime;
    private int _lastWallJumpDir;
    public PlayerMovementData Data;

    private Vector2 _moveInput;
    public float LastPressedJumpTime { get; private set; }

    [Header("Checks")]
    [SerializeField] private Transform _groundCheckPoint;
    // Slightly smaller than the character's width (ground) and height (walls).
    [SerializeField] private Vector2 _groundCheckSize = new Vector2(1f, 0.03f);
    [Space(5)]
    [SerializeField] private Transform _frontWallCheckPoint;
    [SerializeField] private Transform _backWallCheckPoint;
    [SerializeField] private Vector2 _wallCheckSize = new Vector2(0.5f, 1.2f);

    [Header("Layers & Tags")]
    [SerializeField] private LayerMask _groundLayer;

    [Header("Throw Bomb")]
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private Transform bombSpawnPoint;
    [Tooltip("Force multiplier reached at full charge.")]
    [SerializeField] private float maxChargeTime = 2f;
    [Tooltip("Seconds of holding needed for a full charge.")]
    public float maxHoldDownTime = 2.5f;
    private float chargeTime = 0f;
    private bool isCharging = false;
    public NetworkVariable<bool> hasBomb = new NetworkVariable<bool>(false);

    // Owner-written: replicated only when it changes, and the sprite flip follows on every peer.
    private readonly NetworkVariable<bool> _facingRight = new(true,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public bool isFacingRight => _facingRight.Value;

    private InputReader _Input;

    #region Lifecycle

    private void Awake()
    {
        rigidBody2D = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        _transferManager = GetComponent<TransferManager>();

        if (stunIndicator == null)
            stunIndicator = transform.Find("StunIndicator").gameObject;
        if (stunIndicator != null)
            stunIndicator.SetActive(false);

        if (Data == null)
            Debug.LogError("PlayerMovementData is not set in the inspector");

        rigidBody2D.constraints = RigidbodyConstraints2D.FreezeAll;

        IsPlaying.OnValueChanged += (oldValue, newValue) =>
        {
            if (newValue)
                rigidBody2D.constraints = RigidbodyConstraints2D.FreezeRotation;
            else if (isGrounded)
                Freeze();
        };
    }

    private void Start()
    {
        SetGravityScale(Data.gravityScale);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _facingRight.OnValueChanged += OnFacingChanged;
    }

    public override void OnNetworkDespawn()
    {
        _facingRight.OnValueChanged -= OnFacingChanged;
        base.OnNetworkDespawn();
    }

    private void OnFacingChanged(bool previous, bool facingRight)
    {
        spriteRenderer.flipX = facingRight ? needDefaultFlip : !needDefaultFlip;
    }

    private void Freeze()
    {
        rigidBody2D.constraints = RigidbodyConstraints2D.FreezeAll;
        animator.SetBool("isRunning", false);
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
        _Input.JumpEvent += OnJumpUpInput;
        _Input.JumpStartEvent += OnJumpInput;
        _Input.AttackEvent += Punch;
        _Input.ThrowEventStart += OnThrowStart;
        _Input.ThrowEventEnd += OnThrowEnd;
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
        _Input.JumpEvent -= OnJumpUpInput;
        _Input.JumpStartEvent -= OnJumpInput;
        _Input.AttackEvent -= Punch;
        _Input.ThrowEventStart -= OnThrowStart;
        _Input.ThrowEventEnd -= OnThrowEnd;
        _Input = null;
    }

    #endregion

    #region State

    // Alive (bomb), not knocked out, not stunned. Uses replicated state: valid on every peer.
    public bool CanAct()
    {
        return _transferManager.IsAlive.Value
            && !isStun.Value && !isStunHit.Value;
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void ResetPositionClientRpc(Vector3 position)
    {
        if (!IsOwner)
            return;

        transform.position = position;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SetStunServerRpc(bool isStunValue)
    {
        isStun.Value = isStunValue;
        SetStunClientRpc(isStunValue);
    }

    // The stun indicator itself follows isStun in Update.
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void SetStunClientRpc(bool isStunValue)
    {
        if (isStunValue)
        {
            currentStunTime = Data.stunTime;
            rigidBody2D.linearVelocity = new Vector2(0, rigidBody2D.linearVelocity.y);
            rigidBody2D.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        }
        else
        {
            // Movement allowed again after the stun.
            rigidBody2D.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SetStunHitServerRpc(bool isStunValue)
    {
        isStunHit.Value = isStunValue;
    }

    // Owner only: start of a knock-out (punch or trap). Becomes a stun on landing.
    public void Hit()
    {
        LastOnGroundTime = 0;
        animator.SetTrigger("isHit");
        IsJumping = true;
        _isJumpFalling = true;
        SetStunHitServerRpc(true);
    }

    #endregion

    #region Update

    private void OnMove(Vector2 movement)
    {
        if (!IsOwner || !CanAct())
            return;

        _moveInput.x = movement.x;
        _moveInput.y = movement.y;
    }

    private void Update()
    {
        bool showStun = isStun.Value;
        if (stunIndicator.activeSelf != showStun)
            stunIndicator.SetActive(showStun);

        if (!IsOwner)
            return;

        #region TIMERS
        LastOnGroundTime -= Time.deltaTime;
        LastOnWallTime -= Time.deltaTime;
        LastOnWallRightTime -= Time.deltaTime;
        LastOnWallLeftTime -= Time.deltaTime;

        LastPressedJumpTime -= Time.deltaTime;
        #endregion

        if (!_transferManager.IsAlive.Value)
        {
            // Dead: fall straight down.
            rigidBody2D.linearVelocity = new Vector2(0, rigidBody2D.linearVelocity.y);
            rigidBody2D.AddForce(Vector2.down * 10, ForceMode2D.Force);
            return;
        }

        if (!IsPlaying.Value)
            return;

        if (isStun.Value)
        {
            currentStunTime -= Time.deltaTime;
            _moveInput = Vector2.zero;
            if (currentStunTime <= 0f)
            {
                SetStunHitServerRpc(false);
                SetStunServerRpc(false);
                currentStunTime = 0f;
            }
        }

        if (isCharging)
            chargeTime += Time.deltaTime;

        #region INPUT HANDLER
        // A NetworkVariable only sends a value that changed: no more RPC every frame.
        if (_moveInput.x < 0)
            _facingRight.Value = false;
        else if (_moveInput.x > 0)
            _facingRight.Value = true;
        #endregion

        #region COLLISION CHECKS
        if (!IsJumping)
        {
            // Ground check
            if (Physics2D.OverlapBox(_groundCheckPoint.position, _groundCheckSize, 0, _groundLayer))
                LastOnGroundTime = Data.coyoteTime;

            // Wall checks: the check points swap sides when the player turns around.
            if (Physics2D.OverlapBox(_frontWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && isFacingRight && !IsWallJumping)
                LastOnWallRightTime = Data.coyoteTime;

            if (Physics2D.OverlapBox(_backWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && !isFacingRight && !IsWallJumping)
                LastOnWallLeftTime = Data.coyoteTime;

            LastOnWallTime = Mathf.Max(LastOnWallLeftTime, LastOnWallRightTime);
        }
        #endregion

        #region JUMP CHECKS
        if (IsJumping && rigidBody2D.linearVelocity.y < 0)
        {
            IsJumping = false;
            animator.SetBool("isJumping", false);
            animator.SetBool("isFalling", true);

            if (!IsWallJumping)
                _isJumpFalling = true;
        }

        animator.SetBool("isFalling", !IsJumping && rigidBody2D.linearVelocity.y < 0 && !IsSliding);

        if (IsWallJumping && Time.time - _wallJumpStartTime > Data.wallJumpTime)
        {
            IsWallJumping = false;
            IsJumping = false;
        }

        if (LastOnGroundTime > 0 && !IsJumping && !IsWallJumping && !isStunHit.Value && !isStun.Value)
        {
            _isJumpCut = false;
            _isJumpFalling = false;
            animator.SetBool("isFalling", false);
            animator.SetBool("isJumping", false);
        }

        if (CanJump() && LastPressedJumpTime > 0)
        {
            IsJumping = true;
            IsWallJumping = false;
            _isJumpCut = false;
            _isJumpFalling = false;
            Jump();
        }
        else if (CanWallJump() && LastPressedJumpTime > 0)
        {
            IsWallJumping = true;
            IsJumping = false;
            _isJumpCut = false;
            _isJumpFalling = false;
            _wallJumpStartTime = Time.time;
            _lastWallJumpDir = (LastOnWallRightTime > 0) ? -1 : 1;

            WallJump(_lastWallJumpDir);
        }
        #endregion

        #region SLIDE CHECKS
        if (CanSlide() && ((LastOnWallLeftTime > 0 && _moveInput.x < 0) || (LastOnWallRightTime > 0 && _moveInput.x > 0)))
        {
            IsSliding = true;
            animator.SetBool("isRunning", false);
        }
        else
        {
            IsSliding = false;
            animator.SetBool("isRunning", _moveInput.x != 0);
        }
        #endregion

        #region GRAVITY
        if (IsSliding)
        {
            SetGravityScale(0);
        }
        else if (rigidBody2D.linearVelocity.y < 0 && _moveInput.y < 0)
        {
            // Much higher gravity when holding down, capped fall speed.
            SetGravityScale(Data.gravityScale * Data.fastFallGravityMult);
            rigidBody2D.linearVelocity = new Vector2(rigidBody2D.linearVelocity.x, Mathf.Max(rigidBody2D.linearVelocity.y, -Data.maxFastFallSpeed));
        }
        else if (_isJumpCut)
        {
            // Higher gravity when the jump button is released.
            SetGravityScale(Data.gravityScale * Data.jumpCutGravityMult);
            rigidBody2D.linearVelocity = new Vector2(rigidBody2D.linearVelocity.x, Mathf.Max(rigidBody2D.linearVelocity.y, -Data.maxFallSpeed));
        }
        else if ((IsJumping || IsWallJumping || _isJumpFalling) && Mathf.Abs(rigidBody2D.linearVelocity.y) < Data.jumpHangTimeThreshold)
        {
            SetGravityScale(Data.gravityScale * Data.jumpHangGravityMult);
        }
        else if (rigidBody2D.linearVelocity.y < 0)
        {
            // Higher gravity when falling, capped fall speed.
            SetGravityScale(Data.gravityScale * Data.fallGravityMult);
            rigidBody2D.linearVelocity = new Vector2(rigidBody2D.linearVelocity.x, Mathf.Max(rigidBody2D.linearVelocity.y, -Data.maxFallSpeed));
        }
        else
        {
            // Default gravity when standing or moving up.
            SetGravityScale(Data.gravityScale);
        }
        #endregion
    }

    private void FixedUpdate()
    {
        if (!IsOwner)
            return;

        if (_transferManager.IsAlive.Value)
        {
            Run(IsWallJumping ? Data.wallJumpRunLerp : 1);

            if (IsSliding)
                Slide();
        }
    }

    #endregion

    #region Jump

    // Animation event.
    void JumpAnticipation()
    {
        animator.SetTrigger("jumpAnticipation");
    }

    public void Jump()
    {
        // Only one jump per press.
        LastPressedJumpTime = 0;
        LastOnGroundTime = 0;

        // Stronger impulse when falling, so a jump always feels the same.
        float force = Data.jumpForce;
        if (rigidBody2D.linearVelocity.y < 0)
            force -= rigidBody2D.linearVelocity.y;

        rigidBody2D.AddForce(Vector2.up * force, ForceMode2D.Impulse);
        animator.SetBool("isJumping", true);
        animator.SetBool("isFalling", false);
    }

    private void WallJump(int dir)
    {
        // Only one wall jump per press.
        LastPressedJumpTime = 0;
        LastOnGroundTime = 0;
        LastOnWallRightTime = 0;
        LastOnWallLeftTime = 0;

        Vector2 force = new Vector2(Data.wallJumpForce.x * dir, Data.wallJumpForce.y);

        if (Mathf.Sign(rigidBody2D.linearVelocity.x) != Mathf.Sign(force.x))
            force.x -= rigidBody2D.linearVelocity.x;

        // Counter the fall speed so the jump always reaches the same height.
        if (rigidBody2D.linearVelocity.y < 0)
            force.y -= rigidBody2D.linearVelocity.y;

        rigidBody2D.AddForce(force, ForceMode2D.Impulse);
        animator.SetBool("isJumping", true);
    }

    private bool CanJump()
    {
        return LastOnGroundTime > 0 && !IsJumping;
    }

    private bool CanWallJump()
    {
        return LastPressedJumpTime > 0 && LastOnWallTime > 0 && LastOnGroundTime <= 0 && (!IsWallJumping ||
             (LastOnWallRightTime > 0 && _lastWallJumpDir == 1) || (LastOnWallLeftTime > 0 && _lastWallJumpDir == -1));
    }

    private bool CanJumpCut()
    {
        return IsJumping && rigidBody2D.linearVelocity.y > 0;
    }

    private bool CanWallJumpCut()
    {
        return IsWallJumping && rigidBody2D.linearVelocity.y > 0;
    }

    public bool CanSlide()
    {
        return LastOnWallTime > 0 && !IsJumping && !IsWallJumping && LastOnGroundTime <= 0;
    }

    public void OnJumpInput()
    {
        LastPressedJumpTime = Data.jumpInputBufferTime;
    }

    public void OnJumpUpInput()
    {
        if (CanJumpCut() || CanWallJumpCut())
            _isJumpCut = true;
    }

    // Animation event.
    void ResetLand()
    {
        animator.ResetTrigger("land");
    }

    #endregion

    #region Run and slide

    public void SetGravityScale(float scale)
    {
        rigidBody2D.gravityScale = scale;
    }

    private void Run(float lerpAmount)
    {
        float targetSpeed = _moveInput.x * Data.runMaxSpeed;
        // Lerp smooths changes of direction and speed.
        targetSpeed = Mathf.Lerp(rigidBody2D.linearVelocity.x, targetSpeed, lerpAmount);

        float accelRate;
        if (LastOnGroundTime > 0)
            accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? Data.runAccelAmount : Data.runDeccelAmount;
        else
            accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? Data.runAccelAmount * Data.accelInAir : Data.runDeccelAmount * Data.deccelInAir;

        // More acceleration and speed at the apex of a jump: bouncier, more responsive.
        if ((IsJumping || IsWallJumping || _isJumpFalling) && Mathf.Abs(rigidBody2D.linearVelocity.y) < Data.jumpHangTimeThreshold)
        {
            accelRate *= Data.jumpHangAccelerationMult;
            targetSpeed *= Data.jumpHangMaxSpeedMult;
        }

        // Do not slow down a player already moving faster than maxSpeed in the wanted direction.
        if (Data.doConserveMomentum && Mathf.Abs(rigidBody2D.linearVelocity.x) > Mathf.Abs(targetSpeed)
            && Mathf.Sign(rigidBody2D.linearVelocity.x) == Mathf.Sign(targetSpeed)
            && Mathf.Abs(targetSpeed) > 0.01f && LastOnGroundTime < 0)
        {
            accelRate = 0;
        }

        float speedDif = targetSpeed - rigidBody2D.linearVelocity.x;
        rigidBody2D.AddForce(speedDif * accelRate * Vector2.right, ForceMode2D.Force);

        animator.SetBool("isRunning", Mathf.Abs(_moveInput.x) > 0.01f && LastOnGroundTime > 0);
    }

    private void Slide()
    {
        // Same as Run, on the y axis. Clamped to avoid over-corrections.
        float speedDif = Data.slideSpeed - rigidBody2D.linearVelocity.y;
        float movement = speedDif * Data.slideAccel;
        movement = Mathf.Clamp(movement, -Mathf.Abs(speedDif) * (1 / Time.fixedDeltaTime), Mathf.Abs(speedDif) * (1 / Time.fixedDeltaTime));

        rigidBody2D.AddForce(movement * Vector2.up);
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

    #region Punch and knockback

    private void Punch()
    {
        if (CanAct())
            PunchServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void PunchServerRpc()
    {
        // Checked again on the server: the client's copy of the state can lag behind.
        if (CanAct())
            ApplyPunch();
    }

    // Server only.
    private void ApplyPunch()
    {
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(transform.position, punchRadius))
        {
            if (hit.gameObject == gameObject || !hit.CompareTag("Player"))
                continue;

            PlayerCharacter target = hit.GetComponent<PlayerCharacter>();
            if (target == null || !target.CanAct())
                continue;

            Vector2 direction = (hit.transform.position - transform.position).normalized;
            target.KnockOutRpc(new Vector2(
                direction.x * punchForce * Data.knockbackForceX,
                punchForce * Data.knockbackForceY));
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Every peer sees this contact: only the owner reacts. Its ClientNetworkTransform
        // shows the result to the others (it used to be applied once per connected peer).
        if (IsOwner && CanAct())
            TrapPunch(collision);
    }

    private void TrapPunch(Collider2D collision)
    {
        if ((collision.includeLayers.value & LayerMask.GetMask("TrapStun")) == 0)
            return;

        // Pushed away from the trap. The collider's centre is used rather than the transform,
        // whose pivot is not always in the middle of the trap.
        Vector2 away = ((Vector2)transform.position - (Vector2)collision.bounds.center).normalized;

        // Right on top of the centre: no side to pick, so push back the way the player came from.
        if (Mathf.Approximately(away.x, 0f))
            away.x = isFacingRight ? -1f : 1f;

        ApplyKnockback(new Vector2(
            away.x * punchForce * Data.knockbackForceX,
            punchForce * Data.knockbackForceY));

        // The server cannot see this contact reliably (this character is driven by its owner),
        // so the owner reports it and the trap plays its hit sound for everyone.
        ATrap trap = collision.GetComponentInParent<ATrap>();
        if (trap != null && trap.IsSpawned)
            ReportTrapHitServerRpc(trap.NetworkObject);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ReportTrapHitServerRpc(NetworkObjectReference trapReference)
    {
        if (!trapReference.TryGet(out NetworkObject trapObject))
            return;

        ATrap trap = trapObject.GetComponentInChildren<ATrap>();
        if (trap != null)
            trap.OnPlayerHit();
    }

    // Sent by the server (punch) to the owner of the character that was hit.
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void KnockOutRpc(Vector2 force)
    {
        ApplyKnockback(force);
    }

    // Owner only: the force goes where the physics is authoritative (ClientNetworkTransform).
    private void ApplyKnockback(Vector2 force)
    {
        rigidBody2D.AddForce(force, ForceMode2D.Impulse);
        Hit();
    }

    #endregion

    #region Bomb throw

    private void OnThrowStart()
    {
        if (!IsOwner || !hasBomb.Value || BombManager.Instance == null || !BombManager.Instance.IsBombHolder(OwnerClientId))
            return;

        isCharging = true;
        chargeTime = 0f;
    }

    private void OnThrowEnd()
    {
        if (!IsOwner || !isCharging)
            return;

        isCharging = false;

        if (!hasBomb.Value || BombManager.Instance == null || !BombManager.Instance.IsBombHolder(OwnerClientId))
            return;

        LaunchBombServerRpc(chargeTime, isFacingRight);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void LaunchBombServerRpc(float chargeTimeClient, bool facingRight)
    {
        // The client checks hasBomb with a replicated copy that can lag behind:
        // without this server check, two quick presses could spawn two bombs.
        if (!hasBomb.Value || bombPrefab == null || bombSpawnPoint == null)
            return;

        hasBomb.Value = false;

        GameObject bomb = Instantiate(bombPrefab, bombSpawnPoint.position, Quaternion.identity);
        NetworkObject bombNetworkObject = bomb.GetComponent<NetworkObject>();
        if (bombNetworkObject != null)
            bombNetworkObject.Spawn();

        bomb.GetComponent<BombProjectile>().LaunchBomb(OwnerClientId, CalculateHoldDownForce(chargeTimeClient), facingRight ? 1 : -1);
    }

    // The client's charge time is clamped here, so it cannot exceed a full charge.
    private float CalculateHoldDownForce(float holdTime)
    {
        float holdTimeNormalized = Mathf.Clamp01(holdTime / maxHoldDownTime);
        return holdTimeNormalized * maxChargeTime;
    }

    #endregion

    #region Ground contact

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Ground"))
            return;

        if (_isJumpFalling)
        {
            animator.SetBool("isFalling", false);

            if (isStunHit.Value)
            {
                animator.ResetTrigger("isHit");

                // Physics runs on every peer: only the owner turns the knock-out into a stun.
                if (IsOwner)
                {
                    SetStunHitServerRpc(false);
                    SetStunServerRpc(true);
                }
            }
        }

        isGrounded = true;
        animator.SetTrigger("land");

        if (!IsPlaying.Value)
            Freeze();
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            isGrounded = false;
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, punchRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(_groundCheckPoint.position, _groundCheckSize);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(_frontWallCheckPoint.position, _wallCheckSize);
        Gizmos.DrawWireCube(_backWallCheckPoint.position, _wallCheckSize);
    }
}