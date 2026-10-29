using UnityEngine;

// Running, jumping, wall jumping, sliding and gravity of a character. Runs on the owner only:
// its owner-authority NetworkTransform replicates the result. Input comes through PlayerCharacter.
[RequireComponent(typeof(PlayerCharacter))]
public class PlayerMovement : MonoBehaviour
{
    public PlayerMovementData Data;

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

    private PlayerCharacter _character;
    private PlayerCombat _combat;
    private PlayerBomb _bomb;
    private Rigidbody2D _rigidBody;
    private Animator _animator;

    private bool _isGrounded;
    private Vector2 _moveInput;

    // Reused by IsStandingOnSomething: no allocation per frame.
    private readonly ContactPoint2D[] _contacts = new ContactPoint2D[8];

    public bool IsJumping { get; private set; }
    public bool IsWallJumping { get; private set; }
    public bool IsSliding { get; private set; }

    public float LastOnGroundTime { get; private set; }
    public float LastOnWallTime { get; private set; }
    public float LastOnWallRightTime { get; private set; }
    public float LastOnWallLeftTime { get; private set; }
    public float LastPressedJumpTime { get; private set; }

    // Jump
    private bool _isJumpCut;
    private bool _isJumpFalling;

    // Wall jump
    private float _wallJumpStartTime;
    private int _lastWallJumpDir;

    private void Awake()
    {
        _character = GetComponent<PlayerCharacter>();
        _combat = GetComponent<PlayerCombat>();
        _bomb = GetComponent<PlayerBomb>();
        _rigidBody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();

        if (Data == null)
            Debug.LogError("[PlayerMovement] PlayerMovementData is not set in the inspector.");

        _rigidBody.constraints = RigidbodyConstraints2D.FreezeAll;

        _character.IsPlaying.OnValueChanged += (oldValue, newValue) =>
        {
            if (newValue)
                _rigidBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            else if (_isGrounded)
                Freeze();
        };
    }

    private void Start()
    {
        SetGravityScale(Data.gravityScale);
    }

    private void Freeze()
    {
        _rigidBody.constraints = RigidbodyConstraints2D.FreezeAll;
        _animator.SetBool("isRunning", false);
    }

    #region Input (owner, through PlayerCharacter)

    public void OnMove(Vector2 movement)
    {
        _moveInput = movement;
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

    #endregion

    // Owner only (PlayerCombat): the character is sent flying; it falls until it lands.
    public void OnKnockedOut()
    {
        LastOnGroundTime = 0;
        IsJumping = true;
        _isJumpFalling = true;
    }

    private void Update()
    {
        if (!_character.IsOwner)
            return;

        LastOnGroundTime -= Time.deltaTime;
        LastOnWallTime -= Time.deltaTime;
        LastOnWallRightTime -= Time.deltaTime;
        LastOnWallLeftTime -= Time.deltaTime;
        LastPressedJumpTime -= Time.deltaTime;

        if (!_bomb.IsAlive.Value)
        {
            // Dead: fall straight down.
            _rigidBody.linearVelocity = new Vector2(0, _rigidBody.linearVelocity.y);
            _rigidBody.AddForce(Vector2.down * 10, ForceMode2D.Force);
            return;
        }

        if (!_character.IsPlaying.Value)
            return;

        // Knocked out or stunned: no input at all, a jump pressed just before is dropped too.
        if (!_character.CanAct())
        {
            _moveInput = Vector2.zero;
            LastPressedJumpTime = 0;
        }

        // A NetworkVariable only sends a value that changed: no RPC every frame.
        if (_moveInput.x < 0)
            _character.SetFacing(false);
        else if (_moveInput.x > 0)
            _character.SetFacing(true);

        UpdateCollisionChecks();
        UpdateJump();
        UpdateSlide();
        UpdateGravity();
    }

    private void FixedUpdate()
    {
        if (!_character.IsOwner || !_bomb.IsAlive.Value)
            return;

        // Flying after a hit: the knockback is left alone, no air control slows it down.
        if (_combat.IsFlying)
            return;

        Run(IsWallJumping ? Data.wallJumpRunLerp : 1);

        if (IsSliding)
            Slide();
    }

    private void UpdateCollisionChecks()
    {
        if (IsJumping)
            return;

        if (Physics2D.OverlapBox(_groundCheckPoint.position, _groundCheckSize, 0, _groundLayer))
            LastOnGroundTime = Data.coyoteTime;

        // The check points swap sides when the player turns around.
        bool facingRight = _character.isFacingRight;

        if (Physics2D.OverlapBox(_frontWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && facingRight && !IsWallJumping)
            LastOnWallRightTime = Data.coyoteTime;

        if (Physics2D.OverlapBox(_backWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && !facingRight && !IsWallJumping)
            LastOnWallLeftTime = Data.coyoteTime;

        LastOnWallTime = Mathf.Max(LastOnWallLeftTime, LastOnWallRightTime);
    }

    private void UpdateJump()
    {
        if (IsJumping && _rigidBody.linearVelocity.y < 0)
        {
            IsJumping = false;
            _animator.SetBool("isJumping", false);
            _animator.SetBool("isFalling", true);

            if (!IsWallJumping)
                _isJumpFalling = true;
        }

        _animator.SetBool("isFalling", !IsJumping && _rigidBody.linearVelocity.y < 0 && !IsSliding);

        if (IsWallJumping && Time.time - _wallJumpStartTime > Data.wallJumpTime)
        {
            IsWallJumping = false;
            IsJumping = false;
        }

        if (LastOnGroundTime > 0 && !IsJumping && !IsWallJumping && !_combat.IsDown)
        {
            _isJumpCut = false;
            _isJumpFalling = false;
            _animator.SetBool("isFalling", false);
            _animator.SetBool("isJumping", false);
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
    }

    private void UpdateSlide()
    {
        if (CanSlide() && ((LastOnWallLeftTime > 0 && _moveInput.x < 0) || (LastOnWallRightTime > 0 && _moveInput.x > 0)))
        {
            IsSliding = true;
            _animator.SetBool("isRunning", false);
        }
        else
        {
            IsSliding = false;
            _animator.SetBool("isRunning", _moveInput.x != 0);
        }
    }

    private void UpdateGravity()
    {
        if (IsSliding)
        {
            SetGravityScale(0);
        }
        else if (_rigidBody.linearVelocity.y < 0 && _moveInput.y < 0)
        {
            // Much higher gravity when holding down, capped fall speed.
            SetGravityScale(Data.gravityScale * Data.fastFallGravityMult);
            _rigidBody.linearVelocity = new Vector2(_rigidBody.linearVelocity.x, Mathf.Max(_rigidBody.linearVelocity.y, -Data.maxFastFallSpeed));
        }
        else if (_isJumpCut)
        {
            // Higher gravity when the jump button is released.
            SetGravityScale(Data.gravityScale * Data.jumpCutGravityMult);
            _rigidBody.linearVelocity = new Vector2(_rigidBody.linearVelocity.x, Mathf.Max(_rigidBody.linearVelocity.y, -Data.maxFallSpeed));
        }
        else if ((IsJumping || IsWallJumping || _isJumpFalling) && Mathf.Abs(_rigidBody.linearVelocity.y) < Data.jumpHangTimeThreshold)
        {
            SetGravityScale(Data.gravityScale * Data.jumpHangGravityMult);
        }
        else if (_rigidBody.linearVelocity.y < 0)
        {
            // Higher gravity when falling, capped fall speed.
            SetGravityScale(Data.gravityScale * Data.fallGravityMult);
            _rigidBody.linearVelocity = new Vector2(_rigidBody.linearVelocity.x, Mathf.Max(_rigidBody.linearVelocity.y, -Data.maxFallSpeed));
        }
        else
        {
            // Default gravity when standing or moving up.
            SetGravityScale(Data.gravityScale);
        }
    }

    #region Jump

    // Animation event.
    private void JumpAnticipation()
    {
        _animator.SetTrigger("jumpAnticipation");
    }

    private void Jump()
    {
        // Only one jump per press.
        LastPressedJumpTime = 0;
        LastOnGroundTime = 0;

        // Stronger impulse when falling, so a jump always feels the same.
        float force = Data.jumpForce;
        if (_rigidBody.linearVelocity.y < 0)
            force -= _rigidBody.linearVelocity.y;

        _rigidBody.AddForce(Vector2.up * force, ForceMode2D.Impulse);
        _animator.SetBool("isJumping", true);
        _animator.SetBool("isFalling", false);
    }

    private void WallJump(int dir)
    {
        // Only one wall jump per press.
        LastPressedJumpTime = 0;
        LastOnGroundTime = 0;
        LastOnWallRightTime = 0;
        LastOnWallLeftTime = 0;

        Vector2 force = new Vector2(Data.wallJumpForce.x * dir, Data.wallJumpForce.y);

        if (Mathf.Sign(_rigidBody.linearVelocity.x) != Mathf.Sign(force.x))
            force.x -= _rigidBody.linearVelocity.x;

        // Counter the fall speed so the jump always reaches the same height.
        if (_rigidBody.linearVelocity.y < 0)
            force.y -= _rigidBody.linearVelocity.y;

        _rigidBody.AddForce(force, ForceMode2D.Impulse);
        _animator.SetBool("isJumping", true);
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
        return IsJumping && _rigidBody.linearVelocity.y > 0;
    }

    private bool CanWallJumpCut()
    {
        return IsWallJumping && _rigidBody.linearVelocity.y > 0;
    }

    private bool CanSlide()
    {
        return LastOnWallTime > 0 && !IsJumping && !IsWallJumping && LastOnGroundTime <= 0;
    }

    // Animation event.
    private void ResetLand()
    {
        _animator.ResetTrigger("land");
    }

    #endregion

    #region Run and slide

    private void SetGravityScale(float scale)
    {
        _rigidBody.gravityScale = scale;
    }

    private void Run(float lerpAmount)
    {
        float targetSpeed = _moveInput.x * Data.runMaxSpeed;
        // Lerp smooths changes of direction and speed.
        targetSpeed = Mathf.Lerp(_rigidBody.linearVelocity.x, targetSpeed, lerpAmount);

        float accelRate;
        if (LastOnGroundTime > 0)
            accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? Data.runAccelAmount : Data.runDeccelAmount;
        else
            accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? Data.runAccelAmount * Data.accelInAir : Data.runDeccelAmount * Data.deccelInAir;

        // More acceleration and speed at the apex of a jump: bouncier, more responsive.
        if ((IsJumping || IsWallJumping || _isJumpFalling) && Mathf.Abs(_rigidBody.linearVelocity.y) < Data.jumpHangTimeThreshold)
        {
            accelRate *= Data.jumpHangAccelerationMult;
            targetSpeed *= Data.jumpHangMaxSpeedMult;
        }

        // Do not slow down a player already moving faster than maxSpeed in the wanted direction.
        if (Data.doConserveMomentum && Mathf.Abs(_rigidBody.linearVelocity.x) > Mathf.Abs(targetSpeed)
            && Mathf.Sign(_rigidBody.linearVelocity.x) == Mathf.Sign(targetSpeed)
            && Mathf.Abs(targetSpeed) > 0.01f && LastOnGroundTime < 0)
        {
            accelRate = 0;
        }

        float speedDif = targetSpeed - _rigidBody.linearVelocity.x;
        _rigidBody.AddForce(speedDif * accelRate * Vector2.right, ForceMode2D.Force);

        _animator.SetBool("isRunning", Mathf.Abs(_moveInput.x) > 0.01f && LastOnGroundTime > 0);
    }

    private void Slide()
    {
        // Same as Run, on the y axis. Clamped to avoid over-corrections.
        float speedDif = Data.slideSpeed - _rigidBody.linearVelocity.y;
        float movement = speedDif * Data.slideAccel;
        movement = Mathf.Clamp(movement, -Mathf.Abs(speedDif) * (1 / Time.fixedDeltaTime), Mathf.Abs(speedDif) * (1 / Time.fixedDeltaTime));

        _rigidBody.AddForce(movement * Vector2.up);
    }

    #endregion

    #region Ground contact

    // Owner only (PlayerCombat): resting on something solid, whatever its tag or layer (ground,
    // a trap, another player). Triggers give no contact.
    public bool IsStandingOnSomething()
    {
        int count = _rigidBody.GetContacts(_contacts);
        for (int i = 0; i < count; i++)
        {
            if (_contacts[i].normal.y > 0.5f)
                return true;
        }
        return false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Ground"))
            return;

        if (_isJumpFalling)
            _animator.SetBool("isFalling", false);

        _isGrounded = true;
        _animator.SetTrigger("land");

        if (!_character.IsPlaying.Value)
            Freeze();
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            _isGrounded = false;
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        if (_groundCheckPoint == null || _frontWallCheckPoint == null || _backWallCheckPoint == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(_groundCheckPoint.position, _groundCheckSize);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(_frontWallCheckPoint.position, _wallCheckSize);
        Gizmos.DrawWireCube(_backWallCheckPoint.position, _wallCheckSize);
    }

}
