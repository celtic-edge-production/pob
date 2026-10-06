using Unity.Netcode;
using UnityEngine;

// Bomb-related state of one character: alive or dead, short invincibility after passing
// the bomb, and passing the bomb on contact.
public class TransferManager : NetworkBehaviour
{
    public NetworkVariable<bool> IsAlive = new NetworkVariable<bool>(true);

    // Server time until which this player cannot receive the bomb. Replicated, so every peer
    // knows who is invincible (the client-side contact check and any future visual).
    public NetworkVariable<double> InvincibleUntil = new NetworkVariable<double>(0);

    public GameObject holdBombIndicator;
    public Animator animator;

    [Tooltip("Seconds during which a player who just passed the bomb cannot get it back.")]
    public float invincibilityTime = 2f;

    private PlayerCharacter _character;

    private void Awake()
    {
        _character = GetComponent<PlayerCharacter>();
    }

    // Server only.
    public void MarkAsDead()
    {
        if (!IsServer)
            return;

        _character.hasBomb.Value = false;
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
        _character.hasBomb.Value = false;
        ReviveClientRpc();
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

    private void Update()
    {
        // Polled rather than subscribed: this character lives across scenes, while
        // BombManager only exists in game scenes.
        bool showIndicator = IsAlive.Value
            && BombManager.Instance != null
            && BombManager.Instance.IsBombHolder(OwnerClientId);

        if (holdBombIndicator.activeSelf != showIndicator)
            holdBombIndicator.SetActive(showIndicator);
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        // Only the holder's own machine asks for the transfer; the server validates it again
        // (TransferBombServerRpc checks the sender, CanTransferBombTo checks the target).
        if (!IsOwner || !IsAlive.Value || !_character.hasBomb.Value)
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
}