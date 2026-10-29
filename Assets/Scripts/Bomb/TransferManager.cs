using Unity.Netcode;
using UnityEngine;

// Everything about the bomb on one character: holding it, throwing it, passing it on contact,
// the short invincibility after passing it, and being alive or dead.
// (Formerly split between this component and PlayerCharacter.hasBomb / the throw.)
[RequireComponent(typeof(PlayerCharacter))]
public class TransferManager : NetworkBehaviour
{
    public NetworkVariable<bool> IsAlive = new NetworkVariable<bool>(true);

    // True while the bomb is in this character's hands (false once thrown, until caught or picked up).
    public NetworkVariable<bool> HasBomb = new NetworkVariable<bool>(false);

    // Server time until which this player cannot receive the bomb. Replicated, so every peer
    // knows who is invincible (the client-side contact check and any future visual).
    public NetworkVariable<double> InvincibleUntil = new NetworkVariable<double>(0);

    public GameObject holdBombIndicator;
    public Animator animator;

    [Tooltip("Seconds during which a player who just passed the bomb cannot get it back.")]
    public float invincibilityTime = 2f;

    [Header("Throw")]
    [SerializeField] private GameObject _bombPrefab;
    [SerializeField] private Transform _bombSpawnPoint;
    [Tooltip("Force multiplier reached at full charge.")]
    [SerializeField] private float _maxChargeForce = 2f;
    [Tooltip("Seconds of holding needed for a full charge.")]
    [SerializeField] private float _fullChargeTime = 2.5f;

    private PlayerCharacter _character;

    // Owner only.
    private bool _isCharging;
    private float _chargeTime;

    private void Awake()
    {
        _character = GetComponent<PlayerCharacter>();
    }

    #region Life and invincibility (server)

    // Server only.
    public void MarkAsDead()
    {
        if (!IsServer)
            return;

        HasBomb.Value = false;
        IsAlive.Value = false;
        DieClientRpc();
    }

    // Server only.
    public void Revive()
    {
        if (!IsServer)
            return;

        IsAlive.Value = true;
        InvincibleUntil.Value = 0;
        HasBomb.Value = false;
        ReviveClientRpc();

        // A player still knocked out or stunned at the end of the round starts the next one on his feet.
        if (TryGetComponent(out PlayerCombat combat))
            combat.ResetState();
    }

    // Server only: called when this player passes the bomb on.
    public void OnBombTransferred()
    {
        InvincibleUntil.Value = NetworkManager.ServerTime.Time + invincibilityTime;
    }

    public bool IsPlayerInvincible()
    {
        return NetworkManager != null && NetworkManager.ServerTime.Time < InvincibleUntil.Value;
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void DieClientRpc()
    {
        animator.SetTrigger("isDeadGround");
        holdBombIndicator.SetActive(false);
        _character.DesactivateCamera();
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void ReviveClientRpc()
    {
        animator.ResetTrigger("isDeadGround");
        animator.Play("Idle");
        holdBombIndicator.SetActive(false);
    }

    #endregion

    private void Update()
    {
        // Polled rather than subscribed: this character lives across scenes, while
        // BombManager only exists in game scenes.
        bool showIndicator = IsAlive.Value
            && BombManager.Instance != null
            && BombManager.Instance.IsBombHolder(OwnerClientId);

        if (holdBombIndicator.activeSelf != showIndicator)
            holdBombIndicator.SetActive(showIndicator);

        if (!IsOwner || !_isCharging)
            return;

        // Knocked out or stunned while charging: the throw is cancelled.
        if (!_character.CanAct())
            _isCharging = false;
        else
            _chargeTime += Time.deltaTime;
    }

    #region Pass on contact

    private void OnCollisionEnter2D(Collision2D other)
    {
        // Only the holder's own machine asks for the transfer; the server validates it again
        // (TransferBombServerRpc checks the sender, CanTransferBombTo checks the target).
        if (!IsOwner || !IsAlive.Value || !HasBomb.Value)
            return;

        if (!other.gameObject.CompareTag("Player"))
            return;

        if (BombManager.Instance == null || !BombManager.Instance.IsBombHolder(OwnerClientId))
            return;

        TransferManager target = other.gameObject.GetComponent<TransferManager>();
        if (target == null || !target.IsAlive.Value || target.IsPlayerInvincible())
            return;

        BombManager.Instance.TransferBombServerRpc(target.OwnerClientId);
    }

    #endregion

    #region Throw (owner input through PlayerCharacter)

    public void OnThrowStart()
    {
        if (!IsOwner || !_character.IsPlaying.Value || !IsAlive.Value || !CanThrow())
            return;

        _isCharging = true;
        _chargeTime = 0f;
    }

    public void OnThrowEnd()
    {
        if (!IsOwner || !_isCharging)
            return;

        _isCharging = false;

        if (CanThrow())
            LaunchBombServerRpc(_chargeTime, _character.isFacingRight);
    }

    private bool CanThrow()
    {
        return HasBomb.Value && BombManager.Instance != null && BombManager.Instance.IsBombHolder(OwnerClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void LaunchBombServerRpc(float chargeTimeClient, bool facingRight)
    {
        // The client checks HasBomb with a replicated copy that can lag behind:
        // without this server check, two quick presses could spawn two bombs.
        if (!HasBomb.Value || _bombPrefab == null || _bombSpawnPoint == null)
            return;

        // Checked here too: a stunned or knocked-out player cannot throw.
        if (!_character.CanAct())
            return;

        HasBomb.Value = false;

        GameObject bomb = Instantiate(_bombPrefab, _bombSpawnPoint.position, Quaternion.identity);
        NetworkObject bombNetworkObject = bomb.GetComponent<NetworkObject>();
        if (bombNetworkObject != null)
            bombNetworkObject.Spawn(destroyWithScene: true); // Belongs to the level, like every game object.

        bomb.GetComponent<BombProjectile>().LaunchBomb(OwnerClientId, ChargeToForce(chargeTimeClient), facingRight ? 1 : -1);
    }

    // The client's charge time is clamped here, so it cannot exceed a full charge.
    private float ChargeToForce(float chargeTime)
    {
        return Mathf.Clamp01(chargeTime / _fullChargeTime) * _maxChargeForce;
    }

    #endregion

}