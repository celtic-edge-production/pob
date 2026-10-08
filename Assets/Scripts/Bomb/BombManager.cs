using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

// Server-side bomb rules: who holds the bomb, when it explodes, who can receive it.
// The timer display and the music read ExplosionTime on every peer
// (BombTimerDisplay, BombMusic), so no per-frame RPC is needed.
public class BombManager : NetworkSingleton<BombManager>
{
    private const double NoExplosion = -1;

    [Tooltip("Base fuse in seconds, divided by (alive players - 1), plus 0 to 5 random seconds.")]
    public float bombTimer = 60f;

    public NetworkVariable<ulong> bombHolderId = new NetworkVariable<ulong>(ulong.MaxValue);

    // Server time of the explosion, or NoExplosion between bombs. Sent once per bomb:
    // every peer computes the remaining time locally from the synchronized server clock.
    public NetworkVariable<double> ExplosionTime = new NetworkVariable<double>(NoExplosion);

    private ulong oldBombHolderId = ulong.MaxValue;

    public bool IsRoundRunning => ExplosionTime.Value != NoExplosion;

    // Seconds left before the explosion, valid on any peer. Negative when no bomb is running.
    public float RemainingTime => IsRoundRunning && NetworkManager != null
        ? (float)(ExplosionTime.Value - NetworkManager.ServerTime.Time)
        : -1f;

    private void Update()
    {
        if (!IsServer || !IsRoundRunning)
            return;

        if (NetworkManager.ServerTime.Time >= ExplosionTime.Value)
            ExplodeBombHolder();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TransferBombServerRpc(ulong newHolderId, RpcParams rpcParams = default)
    {
        // Only the current holder may pass the bomb: the request comes from the holder's own
        // machine (TransferManager.OnCollisionEnter2D), and the sender is checked again here.
        if (!RpcValidation.SenderMatches(rpcParams, bombHolderId.Value))
            return;

        AssignBombHolder(newHolderId);
    }

    // Server only. Returns true if the bomb actually changed hands.
    public bool AssignBombHolder(ulong newHolderId)
    {
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(newHolderId))
            return false;

        NetworkObject newHolder = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(newHolderId);

        if (!CanTransferBombTo(newHolder, newHolderId))
            return false;

        if (!TryReleaseCurrentHolder())
            return false;

        // First bomb of the round: light the fuse.
        if (!IsRoundRunning)
            StartCountdown();

        GiveBombTo(newHolder, newHolderId);

        oldBombHolderId = bombHolderId.Value;
        bombHolderId.Value = newHolderId;
        return true;
    }

    private bool CanTransferBombTo(NetworkObject newHolder, ulong newHolderId)
    {
        TransferManager target = newHolder != null ? newHolder.GetComponentInChildren<TransferManager>() : null;

        // Checked here, on the server: the client-side checks in TransferManager are only a
        // pre-filter, and a thrown bomb (BombProjectile) has none.
        bool isAlive = target != null && target.IsAlive.Value;
        bool isInvincible = target != null && target.IsPlayerInvincible();

        return CanTransfer(bombHolderId.Value, newHolderId, isAlive, isInvincible);
    }

    // Returns false if the holder is stunned with the bomb in hand: a stunned player cannot
    // pass it on. A bomb already thrown (hasBomb is false) can always change hands.
    private bool TryReleaseCurrentHolder()
    {
        NetworkObject currentHolder = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(bombHolderId.Value);

        // No holder yet: first bomb of the round.
        if (currentHolder == null)
            return true;

        TransferManager holderBomb = currentHolder.GetComponentInChildren<TransferManager>();
        if (holderBomb == null)
            return true;

        PlayerCombat holderCombat = currentHolder.GetComponentInChildren<PlayerCombat>();
        if (holderCombat != null && holderCombat.IsStunned.Value && holderBomb.HasBomb.Value)
            return false;

        holderBomb.HasBomb.Value = false;
        holderBomb.OnBombTransferred();
        return true;
    }

    private void GiveBombTo(NetworkObject newHolder, ulong newHolderId)
    {
        TransferManager newHolderBomb = newHolder != null ? newHolder.GetComponentInChildren<TransferManager>() : null;

        if (newHolderBomb != null)
            newHolderBomb.HasBomb.Value = true;
        else
            Debug.LogWarning("New bomb holder not found: " + newHolderId);
    }

    private void StartCountdown()
    {
        int alivePlayers = GameRoundManager.Instance.players.Count(p => p.IsAlive.Value);
        float duration = bombTimer / MathUtils.SafeDivisor(alivePlayers) + Random.Range(0f, 5f);

        ExplosionTime.Value = NetworkManager.ServerTime.Time + duration;
    }

    private void ExplodeBombHolder()
    {
        NetworkObject player = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(bombHolderId.Value);

        if (player != null)
        {
            TransferManager currentHolder = player.GetComponentInChildren<TransferManager>();

            if (currentHolder != null)
                currentHolder.MarkAsDead();
            else
                Debug.LogWarning("TransferManager not found on player.");
        }

        // StopRound() resets oldBombHolderId, read it first.
        ulong assistId = oldBombHolderId;

        StopRound();

        if (assistId != ulong.MaxValue)
            GameRoundManager.Instance.AddRoundScore(assistId, 2, "Assist");

        GameRoundManager.Instance.CheckForRoundEnd();
    }

    private void DespawnActiveBombs()
    {
        foreach (BombProjectile bombProjectile in FindObjectsByType<BombProjectile>(FindObjectsSortMode.None))
        {
            if (bombProjectile.IsSpawned)
                bombProjectile.NetworkObject.Despawn();
        }
    }

    public static bool CanTransfer(ulong currentHolderId, ulong newHolderId, bool newHolderIsAlive, bool newHolderIsInvincible)
    {
        if (!newHolderIsAlive || newHolderIsInvincible)
            return false;

        return currentHolderId != newHolderId;
    }

    public void HandlePlayerLeft(ulong clientId)
    {
        // No assist points for a player who left.
        if (oldBombHolderId == clientId)
            oldBombHolderId = ulong.MaxValue;

        if (!IsRoundRunning || bombHolderId.Value != clientId)
            return;

        // The holder left with the bomb: give it to a random alive player.
        // The fuse keeps burning (AssignBombHolder only lights it when no bomb is running).
        List<TransferManager> candidates = GameRoundManager.Instance.players
            .Where(p => p.IsAlive.Value && !p.IsPlayerInvincible())
            .ToList();

        bombHolderId.Value = ulong.MaxValue;

        if (candidates.Count > 0)
            AssignBombHolder(candidates[Random.Range(0, candidates.Count)].OwnerClientId);
    }

    // Server only. Clears the current bomb; the display and the music follow ExplosionTime.
    public void StopRound()
    {
        ExplosionTime.Value = NoExplosion;
        bombHolderId.Value = ulong.MaxValue;
        oldBombHolderId = ulong.MaxValue;

        DespawnActiveBombs();
    }

    public bool IsBombHolder(ulong clientId)
    {
        return bombHolderId.Value == clientId;
    }
}