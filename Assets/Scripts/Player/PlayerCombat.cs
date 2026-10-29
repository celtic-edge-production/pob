using Unity.Netcode;
using UnityEngine;

// Punches, knock-outs (punch or trap) and the stun that follows the landing.
// A knocked-out character flies until it lands, then stays stunned for Data.stunTime.
// The owner runs the physics, so the owner decides the landing and the end of the stun with its
// own state (_isFlying, _isStunnedHere), without waiting for the replicated copies: a landing
// before IsKnockedOut came back from the server used to leave the character knocked out forever.
[RequireComponent(typeof(PlayerCharacter), typeof(PlayerMovement))]
public class PlayerCombat : NetworkBehaviour
{
    [SerializeField] private float _punchForce = 5f;
    [SerializeField] private float _punchRadius = 1.0f;
    [SerializeField] private GameObject _stunIndicator;

    [Tooltip("A knock-out lasts at least this long, so the landing check cannot end it on the frame of the hit.")]
    [SerializeField] private float _minFlightTime = 0.15f;
    [Tooltip("Safety net: a knock-out never lasts longer than this, whatever the character landed on.")]
    [SerializeField] private float _maxFlightTime = 2.5f;

    // Flying after a hit (formerly isStunHit), then stunned on the ground (formerly isStun).
    public NetworkVariable<bool> IsKnockedOut = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> IsStunned = new NetworkVariable<bool>(false);

    private PlayerCharacter _character;
    private PlayerMovement _movement;
    private Rigidbody2D _rigidBody;
    private Animator _animator;

    // Owner only: the knock-out and the stun as this machine decided them.
    private bool _isFlying;
    private float _flightTime;
    private bool _isStunnedHere;
    private float _stunTimeLeft;

    // Owner only (PlayerMovement): flying after a hit, no air control.
    public bool IsFlying => _isFlying;

    // Knocked out or stunned. Every peer reads the replicated state; the owner also knows it
    // before the server has answered.
    public bool IsDown => IsKnockedOut.Value || IsStunned.Value || (IsOwner && (_isFlying || _isStunnedHere));

    private PlayerMovementData Data => _movement.Data;

    private void Awake()
    {
        _character = GetComponent<PlayerCharacter>();
        _movement = GetComponent<PlayerMovement>();
        _rigidBody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();

        if (_stunIndicator == null)
        {
            Transform indicator = transform.Find("StunIndicator");
            if (indicator != null)
                _stunIndicator = indicator.gameObject;
        }

        if (_stunIndicator != null)
            _stunIndicator.SetActive(false);
    }

    private void Update()
    {
        // Every peer shows the indicator from the replicated state.
        if (_stunIndicator != null && _stunIndicator.activeSelf != IsStunned.Value)
            _stunIndicator.SetActive(IsStunned.Value);

        if (!IsOwner)
            return;

        if (_isFlying)
            UpdateFlight();
        else if (_isStunnedHere)
            UpdateStun();
    }

    // Owner only. Landed on anything solid (ground, a trap, another player), or flying for too
    // long: the knock-out becomes a stun. It used to wait for a collision with
    // the "Ground" tag, so a character landing on a trap stayed knocked out for good.
    private void UpdateFlight()
    {
        _flightTime += Time.deltaTime;
        if (_flightTime < _minFlightTime)
            return;

        if (_movement.IsStandingOnSomething() || _flightTime >= _maxFlightTime)
            Land();
    }

    private void Land()
    {
        _isFlying = false;
        _isStunnedHere = true;
        _stunTimeLeft = Data.stunTime;
        _animator.ResetTrigger("isHit");

        SetKnockedOutServerRpc(false);
        SetStunnedServerRpc(true);
    }

    private void UpdateStun()
    {
        _stunTimeLeft -= Time.deltaTime;
        if (_stunTimeLeft > 0f)
            return;

        _stunTimeLeft = 0f;
        _isStunnedHere = false;
        SetStunnedServerRpc(false);
    }

    #region Punch

    // Owner, from the attack input (PlayerCharacter).
    public void Punch()
    {
        if (_character.CanAct())
            PunchServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void PunchServerRpc()
    {
        // Checked again on the server: the client's copy of the state can lag behind.
        if (_character.CanAct())
            ApplyPunch();
    }

    // Server only.
    private void ApplyPunch()
    {
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(transform.position, _punchRadius))
        {
            if (hit.gameObject == gameObject || !hit.CompareTag("Player"))
                continue;

            if (!hit.TryGetComponent(out PlayerCharacter target) || !target.CanAct()
                || !hit.TryGetComponent(out PlayerCombat targetCombat))
                continue;

            Vector2 direction = (hit.transform.position - transform.position).normalized;
            targetCombat.KnockOutRpc(new Vector2(
                direction.x * _punchForce * Data.knockbackForceX,
                _punchForce * Data.knockbackForceY));
        }
    }

    // Sent by the server (punch) to the owner of the character that was hit.
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void KnockOutRpc(Vector2 force)
    {
        KnockOut(force);
    }

    #endregion

    #region Traps

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Every peer sees this contact: only the owner reacts. Its NetworkTransform (owner authority)
        // shows the result to the others (it used to be applied once per connected peer).
        if (IsOwner && _character.CanAct())
            HitByTrap(collision);
    }

    private void HitByTrap(Collider2D collision)
    {
        if ((collision.includeLayers.value & LayerMask.GetMask("TrapStun")) == 0)
            return;

        // Arrows are decided by the server (KnockOutFrom): it despawns them on contact, often
        // before they reach this machine, so a client never saw the hit.
        if (collision.GetComponentInParent<Arrow>() != null)
            return;

        // The collider's centre is used rather than the transform, whose pivot is not always in
        // the middle of the trap.
        KnockOut(KnockbackAwayFrom(collision.bounds.center));

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

    // Server only: a hit the server decides (an arrow), sent to the owner, where the physics runs.
    public void KnockOutFrom(Vector2 origin)
    {
        if (IsServer && _character.CanAct())
            KnockOutRpc(KnockbackAwayFrom(origin));
    }

    // Pushed away from the origin of the hit.
    private Vector2 KnockbackAwayFrom(Vector2 origin)
    {
        Vector2 away = ((Vector2)transform.position - origin).normalized;

        // Right on top of it: no side to pick, so push back the way the player came from.
        if (Mathf.Approximately(away.x, 0f))
            away.x = _character.isFacingRight ? -1f : 1f;

        return new Vector2(
            away.x * _punchForce * Data.knockbackForceX,
            _punchForce * Data.knockbackForceY);
    }

    #endregion

    #region Knock-out and stun

    // Owner only: the force goes where the physics is authoritative (NetworkTransform with owner authority).
    private void KnockOut(Vector2 force)
    {
        // Already down here, even if the server has not heard of it yet: a second trap touched
        // during the flight does nothing.
        if (_isFlying || _isStunnedHere)
            return;

        _isFlying = true;
        _flightTime = 0f;

        _rigidBody.AddForce(force, ForceMode2D.Impulse);
        _animator.SetTrigger("isHit");
        _movement.OnKnockedOut();
        SetKnockedOutServerRpc(true);
    }

    // Server only (PlayerBomb.Revive): every new round starts on its feet.
    public void ResetState()
    {
        if (!IsServer)
            return;

        IsKnockedOut.Value = false;
        IsStunned.Value = false;
        ApplyStunClientRpc(false);
        ClearStateOwnerRpc();
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void ClearStateOwnerRpc()
    {
        _isFlying = false;
        _isStunnedHere = false;
        _stunTimeLeft = 0f;
        _animator.ResetTrigger("isHit");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SetKnockedOutServerRpc(bool knockedOut)
    {
        IsKnockedOut.Value = knockedOut;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SetStunnedServerRpc(bool stunned)
    {
        IsStunned.Value = stunned;
        ApplyStunClientRpc(stunned);
    }

    // The stun indicator itself follows IsStunned in Update, the stun time is counted by the owner.
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void ApplyStunClientRpc(bool stunned)
    {
        if (stunned)
        {
            _rigidBody.linearVelocity = new Vector2(0, _rigidBody.linearVelocity.y);
            _rigidBody.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        }
        else
        {
            // Movement allowed again after the stun.
            _rigidBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _punchRadius);
    }

}