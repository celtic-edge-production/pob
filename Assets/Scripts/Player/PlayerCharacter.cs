using System.Collections;
using System.Security.Cryptography;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerCharacter : NetworkBehaviour
{
    [Header("Character do have someones that played me ?")]
    public NetworkVariable<bool> IsPlaying = new(false);

    [Header("Player Settings")]
    public float punchForce = 5;
    public float punchRadius = 1.0f;
    public Camera _playerCamera;
    public bool needDefaultFlip = false;
    private Rigidbody2D rigidBody2D;
    private SpriteRenderer spriteRenderer;
    private BoxCollider2D _Collider;
    private bool isGrounded;
    private Animator animator;

    public bool IsJumping { get; private set; }
    public bool IsWallJumping { get; private set; }
    public bool IsSliding { get; private set; }

    public float LastOnGroundTime { get; private set; }
    public float LastOnWallTime { get; private set; }
    public float LastOnWallRightTime { get; private set; }
    public float LastOnWallLeftTime { get; private set; }

    private GameObject currentBomb;

    public NetworkVariable<bool> isDead = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isDeadHit = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isStun = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> isStunHit = new NetworkVariable<bool>(false);

    public GameObject stunIndicator;
    private float currentStunTime = 0f;

    private TransferManager _transferManager;

    //Jump
    private bool _isJumpCut;
    private bool _isJumpFalling;

    //Wall Jump
    private float _wallJumpStartTime;
    private int _lastWallJumpDir;
    public PlayerMovementData Data;

    private Vector2 _moveInput;
    public float LastPressedJumpTime { get; private set; }

    //Set all of these up in the inspector
    [Header("Checks")]
    [SerializeField] private Transform _groundCheckPoint;
    //Size of groundCheck depends on the size of your character generally you want them slightly small than width (for ground) and height (for the wall check)
    [SerializeField] private Vector2 _groundCheckSize = new Vector2(1f, 0.03f);
    [Space(5)]
    [SerializeField] private Transform _frontWallCheckPoint;
    [SerializeField] private Transform _backWallCheckPoint;
    [SerializeField] private Vector2 _wallCheckSize = new Vector2(0.5f, 1.2f);

    [Header("Layers & Tags")]
    [SerializeField] private LayerMask _groundLayer;

    // Variables pour lancer la bombe
    [Header("Throw Bomb")]
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private Transform bombSpawnPoint;
    [SerializeField] private float maxChargeTime = 2f; // Temps max pour charger
    private float chargeTime = 0f; // Temps de charge actuel
    private bool isCharging = false;
    public NetworkVariable<bool> hasBomb = new NetworkVariable<bool>(false);
    public float maxHoldDownTime = 2.5f;
    public bool isFacingRight = true;

    public LayerMask trapStunLayer;

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
            {
                rigidBody2D.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
            else if (!newValue && isGrounded)
            {
                Freeze();
            }
        };
    }

    private void Start()
    {
        SetGravityScale(Data.gravityScale);
    }

    private void Freeze()
    {
        rigidBody2D.constraints = RigidbodyConstraints2D.FreezeAll;

        animator.SetBool("isRunning", false);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void ResetPositionClientRpc(Vector3 position)
    {
        if (!IsOwner)
            return;
        transform.position = position;
        //rigidBody2D.linearVelocityX = 0;
    }

    public bool GetHasBomb()
    {
        return hasBomb.Value;
    }

    private void OnMove(Vector2 movement)
    {
        if (!IsOwner || isDead.Value || isDeadHit.Value || !_transferManager.IsAlive.Value || isStun.Value || isStunHit.Value)
            return;
        _moveInput.x = movement.x;
        _moveInput.y = movement.y;
    }

    private void Update()
    {
        if (isStun.Value && stunIndicator.activeSelf == false)
        {
            stunIndicator.SetActive(true);
        }
        else if (!isStun.Value && stunIndicator.activeSelf == true)
        {
            stunIndicator.SetActive(false);
        }
        if (!IsOwner) return;

        #region TIMERS
        LastOnGroundTime -= Time.deltaTime;
        LastOnWallTime -= Time.deltaTime;
        LastOnWallRightTime -= Time.deltaTime;
        LastOnWallLeftTime -= Time.deltaTime;

        LastPressedJumpTime -= Time.deltaTime;
        #endregion

        if (!_transferManager.IsAlive.Value)
        {
            // Make the player fall, but not in x axis
            rigidBody2D.linearVelocity = new Vector2(0, rigidBody2D.linearVelocity.y);
            rigidBody2D.AddForce(Vector2.down * 10, ForceMode2D.Force);
            return;
        }

        if (isDead.Value || !IsPlaying.Value || isDeadHit.Value || !_transferManager.IsAlive.Value) return;

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
        {
            chargeTime += Time.deltaTime;
            Debug.Log("Player " + OwnerClientId + " is charging the bomb. Charge time: " + chargeTime);
        }

        #region INPUT HANDLER

        if (_moveInput.x < 0)
        {
            spriteRenderer.flipX = !needDefaultFlip;
            isFacingRight = false;
            if (IsServer)
                FlipStateClientRpc(spriteRenderer.flipX, false);
            else
                FlipStateServerRpc(spriteRenderer.flipX, false);
        }
        else if (_moveInput.x > 0)
        {
            spriteRenderer.flipX = needDefaultFlip;
            isFacingRight = true;
            if (IsServer)
                FlipStateClientRpc(spriteRenderer.flipX, true);
            else
                FlipStateServerRpc(spriteRenderer.flipX, true);
        }

        #endregion

        #region COLLISION CHECKS

        if (!IsJumping)
        {
            //Ground Check
            if (Physics2D.OverlapBox(_groundCheckPoint.position, _groundCheckSize, 0, _groundLayer) && !IsJumping) //checks if set box overlaps with ground
            {
                LastOnGroundTime = Data.coyoteTime; //if so sets the lastGrounded to coyoteTime
            }

            //Right Wall Check
            if (((Physics2D.OverlapBox(_frontWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && isFacingRight)) && !IsWallJumping)
                LastOnWallRightTime = Data.coyoteTime;

            //Right Wall Check
            if (((Physics2D.OverlapBox(_backWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && !isFacingRight)) && !IsWallJumping)
                LastOnWallLeftTime = Data.coyoteTime;

            //Two checks needed for both left and right walls since whenever the play turns the wall checkPoints swap sides
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

        if (!IsJumping && rigidBody2D.linearVelocity.y < 0 && !IsSliding)
        {
            animator.SetBool("isFalling", true);
        }
        else
        {
            animator.SetBool("isFalling", false);
        }

        if (IsWallJumping && Time.time - _wallJumpStartTime > Data.wallJumpTime)
        {
            IsWallJumping = false;
            IsJumping = false; // Reset jumping state after wall jump ends
        }

        if (LastOnGroundTime > 0 && !IsJumping && !IsWallJumping && !isStunHit.Value && !isStun.Value)
        {
            _isJumpCut = false;
            animator.SetBool("isFalling", false);

            IsJumping = false; // Reset jumping state when landing on the ground
            animator.SetBool("isJumping", false);

            if (!IsJumping)
            {
                _isJumpFalling = false;
            }
        }

        //Jump
        if (CanJump() && LastPressedJumpTime > 0)
        {
            IsJumping = true;
            IsWallJumping = false;
            _isJumpCut = false;
            _isJumpFalling = false;
            Jump();
        }
        //WALL JUMP
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
        //Higher gravity if we've released the jump input or are falling
        if (IsSliding)
        {
            SetGravityScale(0);
        }
        else if (rigidBody2D.linearVelocity.y < 0 && _moveInput.y < 0)
        {
            //Much higher gravity if holding down
            SetGravityScale(Data.gravityScale * Data.fastFallGravityMult);
            //Caps maximum fall speed, so when falling over large distances we don't accelerate to insanely high speeds
            rigidBody2D.linearVelocity = new Vector2(rigidBody2D.linearVelocity.x, Mathf.Max(rigidBody2D.linearVelocity.y, -Data.maxFastFallSpeed));
        }
        else if (_isJumpCut)
        {
            //Higher gravity if jump button released
            SetGravityScale(Data.gravityScale * Data.jumpCutGravityMult);
            rigidBody2D.linearVelocity = new Vector2(rigidBody2D.linearVelocity.x, Mathf.Max(rigidBody2D.linearVelocity.y, -Data.maxFallSpeed));
        }
        else if ((IsJumping || IsWallJumping || _isJumpFalling) && Mathf.Abs(rigidBody2D.linearVelocity.y) < Data.jumpHangTimeThreshold)
        {
            SetGravityScale(Data.gravityScale * Data.jumpHangGravityMult);
        }
        else if (rigidBody2D.linearVelocity.y < 0)
        {
            //Higher gravity if falling
            SetGravityScale(Data.gravityScale * Data.fallGravityMult);
            //Caps maximum fall speed, so when falling over large distances we don't accelerate to insanely high speeds
            rigidBody2D.linearVelocity = new Vector2(rigidBody2D.linearVelocity.x, Mathf.Max(rigidBody2D.linearVelocity.y, -Data.maxFallSpeed));
        }
        else
        {
            //Default gravity if standing on a platform or moving upwards
            SetGravityScale(Data.gravityScale);
        }
        #endregion
    }

    #region JUMP METHODS

    void JumpAnticipation()
    {
        animator.SetTrigger("jumpAnticipation");
    }

    public void Jump()
    {
        //Ensures we can't call Jump multiple times from one press
        LastPressedJumpTime = 0;
        LastOnGroundTime = 0;

        #region Perform Jump
        //We increase the force applied if we are falling
        //This means we'll always feel like we jump the same amount
        //(setting the player's Y velocity to 0 beforehand will likely work the same, but I find this more elegant :D)
        float force = Data.jumpForce;
        if (rigidBody2D.linearVelocity.y < 0)
            force -= rigidBody2D.linearVelocity.y;

        rigidBody2D.AddForce(Vector2.up * force, ForceMode2D.Impulse);
        animator.SetBool("isJumping", true);
        animator.SetBool("isFalling", false);
        #endregion
    }

    private void WallJump(int dir)
    {
        //Ensures we can't call Wall Jump multiple times from one press
        LastPressedJumpTime = 0;
        LastOnGroundTime = 0;
        LastOnWallRightTime = 0;
        LastOnWallLeftTime = 0;

        #region Perform Wall Jump
        Vector2 force = new Vector2(Data.wallJumpForce.x, Data.wallJumpForce.y);
        force.x *= dir; //apply force in opposite direction of wall

        if (Mathf.Sign(rigidBody2D.linearVelocity.x) != Mathf.Sign(force.x))
            force.x -= rigidBody2D.linearVelocity.x;

        if (rigidBody2D.linearVelocity.y < 0) //checks whether player is falling, if so we subtract the velocity.y (counteracting force of gravity). This ensures the player always reaches our desired jump force or greater
            force.y -= rigidBody2D.linearVelocity.y;

        //Unlike in the run we want to use the Impulse mode.
        //The default mode will apply are force instantly ignoring masss
        rigidBody2D.AddForce(force, ForceMode2D.Impulse);
        animator.SetBool("isJumping", true);
        #endregion
    }
    #endregion

    #region CHECK METHODS

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
        if (LastOnWallTime > 0 && !IsJumping && !IsWallJumping && LastOnGroundTime <= 0)
            return true;
        else
            return false;
    }
    #endregion

    #region INPUT CALLBACKS
    //Methods which whandle input detected in Update()
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

    #region GENERAL METHODS
    public void SetGravityScale(float scale)
    {
        rigidBody2D.gravityScale = scale;
    }
    #endregion

    #region RUN METHODS
    private void Run(float lerpAmount)
    {
        //Calculate the direction we want to move in and our desired velocity
        float targetSpeed = _moveInput.x * Data.runMaxSpeed;
        //We can reduce are control using Lerp() this smooths changes to are direction and speed
        targetSpeed = Mathf.Lerp(rigidBody2D.linearVelocity.x, targetSpeed, lerpAmount);

        #region Calculate AccelRate
        float accelRate;

        //Gets an acceleration value based on if we are accelerating (includes turning)
        //or trying to decelerate (stop). As well as applying a multiplier if we're air borne.
        if (LastOnGroundTime > 0)
            accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? Data.runAccelAmount : Data.runDeccelAmount;
        else
            accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? Data.runAccelAmount * Data.accelInAir : Data.runDeccelAmount * Data.deccelInAir;
        #endregion

        #region Add Bonus Jump Apex Acceleration
        //Increase are acceleration and maxSpeed when at the apex of their jump, makes the jump feel a bit more bouncy, responsive and natural
        if ((IsJumping || IsWallJumping || _isJumpFalling) && Mathf.Abs(rigidBody2D.linearVelocity.y) < Data.jumpHangTimeThreshold)
        {
            accelRate *= Data.jumpHangAccelerationMult;
            targetSpeed *= Data.jumpHangMaxSpeedMult;
        }
        #endregion

        #region Conserve Momentum
        //We won't slow the player down if they are moving in their desired direction but at a greater speed than their maxSpeed
        if (Data.doConserveMomentum && Mathf.Abs(rigidBody2D.linearVelocity.x) > Mathf.Abs(targetSpeed) && Mathf.Sign(rigidBody2D.linearVelocity.x) == Mathf.Sign(targetSpeed) && Mathf.Abs(targetSpeed) > 0.01f && LastOnGroundTime < 0)
        {
            //Prevent any deceleration from happening, or in other words conserve are current momentum
            //You could experiment with allowing for the player to slightly increae their speed whilst in this "state"
            accelRate = 0;
        }
        #endregion

        //Calculate difference between current velocity and desired velocity
        float speedDif = targetSpeed - rigidBody2D.linearVelocity.x;
        //Calculate force along x-axis to apply to thr player

        float movement = speedDif * accelRate;

        //Convert this to a vector and apply to rigidbody
        rigidBody2D.AddForce(movement * Vector2.right, ForceMode2D.Force);
        if (Mathf.Abs(_moveInput.x) > 0.01f && LastOnGroundTime > 0)
        {
            animator.SetBool("isRunning", true);
        }
        else
        {
            animator.SetBool("isRunning", false); // Turn off running animation when player stops
        }
        /*
		 * For those interested here is what AddForce() will do
		 * RB.velocity = new Vector2(RB.velocity.x + (Time.fixedDeltaTime  * speedDif * accelRate) / RB.mass, RB.velocity.y);
		 * Time.fixedDeltaTime is by default in Unity 0.02 seconds equal to 50 FixedUpdate() calls per second
		*/
    }
    #endregion

    #region OTHER MOVEMENT METHODS
    private void Slide()
    {
        //Works the same as the Run but only in the y-axis
        //THis seems to work fine, buit maybe you'll find a better way to implement a slide into this system
        float speedDif = Data.slideSpeed - rigidBody2D.linearVelocity.y;
        float movement = speedDif * Data.slideAccel;
        //So, we clamp the movement here to prevent any over corrections (these aren't noticeable in the Run)
        //The force applied can't be greater than the (negative) speedDifference * by how many times a second FixedUpdate() is called. For more info research how force are applied to rigidbodies.
        movement = Mathf.Clamp(movement, -Mathf.Abs(speedDif) * (1 / Time.fixedDeltaTime), Mathf.Abs(speedDif) * (1 / Time.fixedDeltaTime));

        rigidBody2D.AddForce(movement * Vector2.up);
    }
    #endregion

    private void FixedUpdate()
    {
        if (!IsOwner) return;

        if (!isDeadHit.Value && !isDead.Value && _transferManager.IsAlive.Value)
        {
            if (IsWallJumping)
                Run(Data.wallJumpRunLerp);
            else
                Run(1);

            //Handle Slide
            if (IsSliding)
                Slide();
        }
    }

    public void ActivateCamera()
    {
        if (!IsOwner) return;
        // Get camera with MainCamera tag
        Debug.Log("Activate Camera");
        _playerCamera.gameObject.SetActive(true);
    }

    public void DesactivateCamera()
    {
        if (!IsOwner) return;
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            _playerCamera.gameObject.SetActive(false);
            //mainCamera.gameObject.SetActive(true);
        }
    }

    void Punch()
    {
        if (!isDead.Value && !isDeadHit.Value) {
            if (IsClient)
                PuchServerRpc();
            else if (IsServer)
                ApplyPunch();
        }
    }

    private void OnThrowStart()
    {
        if (BombManager.Instance != null && BombManager.Instance.IsBombHolder(OwnerClientId)) {
            if (IsOwner && hasBomb.Value) {
                isCharging = true;
                chargeTime = 0f;
            }
        }
    }

    private void OnThrowEnd()
    {
        if (BombManager.Instance != null && BombManager.Instance.IsBombHolder(OwnerClientId)) {
            if (IsOwner && hasBomb.Value) {
                if (isCharging) {
                    LaunchBombServerRpc(chargeTime);

                    isCharging = false;
                }
            }
        }
    }

    [Rpc(SendTo.Server)]
    void PuchServerRpc()
    {
        ApplyPunch();
    }

    void ApplyPunch()
    {
        Collider2D[] hitPlayers = Physics2D.OverlapCircleAll(transform.position, punchRadius);

        foreach (Collider2D player in hitPlayers)
        {
            if (player != null && player.gameObject != this.gameObject && player.CompareTag("Player"))
            {
                Rigidbody2D enemyRigidbody = player.GetComponent<Rigidbody2D>();
                PlayerCharacter enemyPlayer = player.GetComponent<PlayerCharacter>();

                if (enemyRigidbody != null && enemyPlayer != null && !enemyPlayer.IsDead() && !enemyPlayer.isDeadHit.Value && !enemyPlayer.isStun.Value && !enemyPlayer.isStunHit.Value)
                {
                    // Calculate the horizontal direction of the knockback
                    Vector2 punchDirection = (player.transform.position - transform.position).normalized;

                    // Adjust the direction for a curved trajectory
                    Vector2 knockbackForce = new Vector2(
                        punchDirection.x * punchForce * Data.knockbackForceX,
                        punchForce * Data.knockbackForceY
                    );

                    // Call the ClientRpc to apply the knockback on the client
                    enemyPlayer.KnockOutClientRpc(knockbackForce);
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Check if the player is punchable
        if (!isDeadHit.Value && !isDead.Value && _transferManager.IsAlive.Value && !isStun.Value && !isStunHit.Value)
        {
            //Debug.Log("Player can be punched by the trapStun");

            TrapPunch(collision);
            //TryTeleportPlayer(collision);
        }
    }

    private void TrapPunch(Collider2D collision)
    {
        // If the arrow is colliding an object that is a TrapStun
        if ((collision.includeLayers.value & LayerMask.GetMask("TrapStun")) != 0)
        {
            Debug.Log("Player is punchable by the trapStun");
            // Calcul de la direction du punch
            Vector2 punchDirection = (collision.transform.position - transform.position).normalized;

            Vector2 knockbackForce = new Vector2(
                punchDirection.x * punchForce * Data.knockbackForceX,
                punchForce * Data.knockbackForceY
            );

            // Appliquer le knockback via RPC
            KnockOutClientRpc(knockbackForce);

            Debug.Log("Punch Player by Trap with Knockback: " + knockbackForce);
        }
    }

    private void TryTeleportPlayer(Collider2D collision)
    {
        LayerMask trapStunLayer = LayerMask.GetMask("Seashell");

        // Check if the layer of the collision is a trapStun layer
        if (trapStunLayer == collision.includeLayers.value)
        {
            GameObject gameObject = collision.gameObject;
            if (gameObject == null)
            {
                Debug.LogError("TryTeleportPlayer Is Null");
                return;
            }

            Seashell seashell = gameObject.GetComponent<Seashell>();
            if (seashell == null)
            {
                Debug.LogError("TryTeleportPlayer SeashellTest Is Null");
                return;
            }

            // Get the network Id of the seashell
            NetworkObject seashellNetworkObject = gameObject.GetComponent<NetworkObject>();

            Debug.Log("seashellNetworkObject Id" + seashellNetworkObject.NetworkObjectId);

            TeleportThePlayerServerRpc(seashellNetworkObject.NetworkObjectId);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void TeleportThePlayerServerRpc(ulong id)
    {
        // Check if the object exists in the SpawnedObjects map
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject seaShellNetworkObject) && seaShellNetworkObject != null)
        {
            // Try to get the Seashell component
            Seashell seashell = seaShellNetworkObject.GetComponent<Seashell>();
            if (seashell != null)
            {
                Debug.Log("Seashell found, teleporting player..." + this.gameObject);

                //Get the player prefab Id
                NetworkManager.Singleton.ConnectedClients.TryGetValue(OwnerClientId, out NetworkClient playerNetworkObject);

                seashell.TeleportPlayerServerRpc(playerNetworkObject.ClientId);
            }
            else
            {
                Debug.LogError("NetworkObject exists but does not have a Seashell component.");
            }
        }
        else
        {
            Debug.LogError($"NetworkObject with id {id} not found or null.");
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void KnockOutClientRpc(Vector2 force)
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            Debug.Log("Punch Direction: " + force);

            rb.AddForce(force, ForceMode2D.Impulse);
        }

        // Garde ajoutee : Hit() envoie un ServerRpc qui met a jour NOTRE propre etat
        // (isStunHit). Comme ce ClientRpc est recu par TOUS les clients (pas seulement
        // le joueur touche), sans cette garde chaque client appelait ce ServerRpc en
        // double/triple. C'est ce qui justifiait a tort le RequireOwnership = false
        // sur SetStunHitServerRpc plus bas.
        if (!IsOwner)
            return;

        Hit();
    }

    [Rpc(SendTo.Server)]
    void FlipStateServerRpc(bool state, bool facing)
    {
        // make the change local on the server
        spriteRenderer.flipX = state;
        isFacingRight = facing;

        // forward the change also to all clients
        FlipStateClientRpc(state, facing);
    }

    // invoked by the server only but executed on ALL clients
    [Rpc(SendTo.ClientsAndHost)]
    void FlipStateClientRpc(bool state, bool facing)
    {
        // skip this function on the LocalPlayer
        // because he is the one who originally invoked this
        if (IsOwner) return;
        isFacingRight = facing;

        //make the change local on all clients
        spriteRenderer.flipX = state;
    }

    // ServerRpc to update isDead
    // RequireOwnership = false retire : depuis les gardes IsOwner ajoutees dans
    // KnockOutClientRpc/DeadHit/OnCollisionEnter2D, seul le proprietaire appelle ce RPC.
    // NGO refuse desormais nativement tout appel venant d'un autre client.
    [Rpc(SendTo.Server)]
    private void SetDeadServerRpc(bool isDeadValue)
    {
        isDead.Value = isDeadValue;
    }

    [Rpc(SendTo.Server)]
    private void SetDeadHitServerRpc(bool isDeadValue)
    {
        isDeadHit.Value = isDeadValue;
    }

    [Rpc(SendTo.Server)]
    private void SetStunServerRpc(bool isStunValue)
    {
        isStun.Value = isStunValue;
        SetStunClientRpc(isStunValue);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SetStunClientRpc(bool isStunValue)
    {
        Debug.Log("SetStunClientRpc: " + isStunValue);
        stunIndicator.SetActive(isStunValue);

        if (isStunValue)
        {
            currentStunTime = Data.stunTime;
            rigidBody2D.linearVelocity = new Vector2(0, rigidBody2D.linearVelocity.y);
            rigidBody2D.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        }
        else
        {
            // R�activer les mouvements apr�s le stun
            rigidBody2D.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    [Rpc(SendTo.Server)]
    private void SetStunHitServerRpc(bool isStunValue)
    {
        isStunHit.Value = isStunValue;
    }

    public void Hit()
    {
        LastOnGroundTime = 0;
        animator.SetTrigger("isHit");
        IsJumping = true;
        _isJumpFalling = true;
        SetStunHitServerRpc(true);
    }

    public void DeadHit()
    {
        animator.SetTrigger("isDeadHit");

        // Meme raison que dans KnockOutClientRpc : cette methode est appelee via un
        // Animation Event, qui se declenche sur chaque client jouant l'animation,
        // pas seulement le joueur concerne.
        if (!IsOwner)
            return;

        SetDeadHitServerRpc(true);
    }

    public bool IsDead()
    {
        return isDead.Value || isDeadHit.Value;
    }

    void ResetLand()
    {
        animator.ResetTrigger("land");
    }

    public void ReceiveBomb(GameObject bomb)
    {
        currentBomb = bomb;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            if (_isJumpFalling)
            {
                //animator.ResetTrigger("fall");
                animator.SetBool("isFalling", false);

                if (isStunHit.Value)
                {
                    animator.ResetTrigger("isHit");

                    // Ce callback physique tourne sur TOUS les clients (chacun simule sa
                    // propre physique locale), pas seulement le proprietaire. Sans cette
                    // garde, chaque client appelait ces ServerRpc en double/triple.
                    if (IsOwner)
                    {
                        SetStunHitServerRpc(false);
                        // ServerRpc to update isDead
                        if (IsServer)
                        {
                            isStun.Value = true;
                            SetStunClientRpc(true);
                        } else
                            SetStunServerRpc(true);
                    }
                }
            }
            else if (isDeadHit.Value)
            {
                animator.ResetTrigger("isDeadHit");

                if (IsOwner)
                    SetDeadHitServerRpc(false);

                animator.SetTrigger("isDeadGround");

                // ServerRpc to update isDead
                if (IsOwner)
                    SetDeadServerRpc(true);

                rigidBody2D.linearVelocity = new Vector2(0, rigidBody2D.linearVelocity.y);
                //StartCoroutine(ResetDeadGroundTrigger());
            }
            isGrounded = true;
            animator.SetTrigger("land");
            if (!IsPlaying.Value)
                Freeze();
        }
    }

    IEnumerator ResetDeadGroundTrigger()
    {
        yield return new WaitForSeconds(2f);

        animator.ResetTrigger("isDeadGround");
        animator.Play("Idle");
        SetDeadServerRpc(false);
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            isGrounded = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, punchRadius);


        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(_groundCheckPoint.position, _groundCheckSize);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(_frontWallCheckPoint.position, _wallCheckSize);
        Gizmos.DrawWireCube(_backWallCheckPoint.position, _wallCheckSize);
    }

    // FAILLE NON CORRIGEE (a dessein) : RequireOwnership = false + aucune verification
    // signifie que n'importe quel client peut appeler ce RPC directement sur le
    // PlayerCharacter de N'IMPORTE QUEL AUTRE joueur, avec N'IMPORTE QUEL senderId,
    // et voler sa propriete. J'ai tente d'ajouter une verification
    // "rpcParams.Receive.SenderClientId == senderId", mais le seul appelant legitime
    // (PlayerIdentity.RequestOwnerShipServerRpc, ligne 169 de Player/PlayerCharacter.cs)
    // appelle cette methode directement en C# alors qu'il tourne deja cote serveur -
    // pas un vrai aller-retour reseau - donc rpcParams.Receive.SenderClientId y resterait
    // a sa valeur par defaut et la verification aurait casse la selection de personnage
    // pour la quasi-totalite des joueurs. Corriger ca proprement demande de restructurer
    // l'appel (faire passer le vrai ServerRpcParams du RPC externe jusqu'ici, ou changer
    // cette methode en simple methode C# non-RPC puisqu'elle n'est jamais appelee par un
    // client distant dans le flux prevu). Je n'ai pas voulu trancher ca sans pouvoir tester.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestOwnerShipServerRpc(ulong senderId, RpcParams rpcParams = default)
    {
        NetworkObject.ChangeOwnership(senderId);

        IsPlaying.Value = true;

        InitKeyBindingClientRPC();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RemoveOwnerShipServerRpc(RpcParams rpcParams = default)
    {
        IsPlaying.Value = false;

        RemoveKeyBindingClientRpc();

        NetworkObject.RemoveOwnership();
    }

    [Rpc(SendTo.Server)]
    private void LaunchBombServerRpc(float chargeTimeClient)
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

        bomb.GetComponent<BombProjectile>().LaunchBomb(OwnerClientId, CalculateHoldDownForce(chargeTimeClient), isFacingRight ? 1 : -1);
    }

    private float CalculateHoldDownForce(float holdTime)
    {
        // Normaliser le temps de charge (entre 0 et 1)
        float holdTimeNormalized = Mathf.Clamp01(holdTime / maxHoldDownTime);

        // Calculer la force totale en fonction du temps de charge
        float force = holdTimeNormalized * maxChargeTime;

        Debug.Log("holdTime -> " + holdTime);
        Debug.Log("maxHoldDownTime -> " + maxHoldDownTime);
        Debug.Log("holdTimeNormalized -> " + holdTimeNormalized);
        Debug.Log("Calculated Force -> " + force);

        return force;
    }

    #region KeyBinding

    private InputReader _Input;

    [Rpc(SendTo.ClientsAndHost)]
    private void InitKeyBindingClientRPC()
    {
        GameObject parent = transform.parent.gameObject;

        if (parent.TryGetComponent(out PlayerIdentity player) && IsOwner) {
            _Input = player.Input;

            _Input.MoveEvent       += OnMove;
            _Input.JumpEvent       += OnJumpUpInput;
            _Input.JumpStartEvent  += OnJumpInput;
            _Input.AttackEvent     += Punch;
            _Input.ThrowEventStart += OnThrowStart;
            _Input.ThrowEventEnd   += OnThrowEnd;
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void RemoveKeyBindingClientRpc()
    {
        if (_Input != null) {
            _Input.MoveEvent       -= OnMove;
            _Input.JumpEvent       -= OnJumpUpInput;
            _Input.JumpStartEvent  -= OnJumpInput;
            _Input.AttackEvent     -= Punch;
            _Input.ThrowEventStart -= OnThrowStart;
            _Input.ThrowEventEnd   -= OnThrowEnd;

            _Input = null;
        }
    }

    #endregion
}
