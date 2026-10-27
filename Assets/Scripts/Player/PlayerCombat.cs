using Unity.Netcode;
using UnityEngine;

// Punches, knock-outs (punch or trap) and the stun that follows the landing.
// A knocked-out character flies until it lands, then stays stunned for Data.stunTime.
[RequireComponent(typeof(PlayerCharacter), typeof(PlayerMovement))]
public class PlayerCombat : NetworkBehaviour
{
    [SerializeField] private float _punchForce = 5f;
    [SerializeField] private float _punchRadius = 1.0f;
    [SerializeField] private GameObject _stunIndicator;

    // Flying after a hit (formerly isStunHit), then stunned on the ground (formerly isStun).
    public NetworkVariable<bool> IsKnockedOut = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> IsStunned = new NetworkVariable<bool>(false);

    private PlayerCharacter _character;
    private PlayerMovement _movement;
    private Rigidbody2D _rigidBody;
    private Animator _animator;

    // Owner only.
    private float _stunTimeLeft;

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

        if (!IsOwner || !IsStunned.Value)
            return;

        _stunTimeLeft -= Time.deltaTime;
        if (_stunTimeLeft <= 0f)
        {
            _stunTimeLeft = 0f;
            SetKnockedOutServerRpc(false);
            SetStunnedServerRpc(false);
        }
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
        _rigidBody.AddForce(force, ForceMode2D.Impulse);
        _animator.SetTrigger("isHit");
        _movement.OnKnockedOut();
        SetKnockedOutServerRpc(true);
    }

    // Every peer (physics runs everywhere), from PlayerMovement when a fall ends on the ground.
    public void OnLandedAfterFall()
    {
        if (!IsKnockedOut.Value)
            return;

        _animator.ResetTrigger("isHit");

        // Only the owner turns the knock-out into a stun.
        if (IsOwner)
        {
            SetKnockedOutServerRpc(false);
            SetStunnedServerRpc(true);
        }
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

    // The stun indicator itself follows IsStunned in Update.
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void ApplyStunClientRpc(bool stunned)
    {
        if (stunned)
        {
            _stunTimeLeft = Data.stunTime;
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