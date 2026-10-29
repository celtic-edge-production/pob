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

    // No holder, no assist.
    public const ulong NoPlayer = ulong.MaxValue;

    // Server only: the holder has just exploded (his client id). One bomb per round: GameRoundManager
    // takes his gold and ends the round.
    public event System.Action<ulong> BombExploded;

    [Header("Fuse (seconds)")]
    [Tooltip("The fuse of a round: one bomb per round, so a round lasts about this long.")]
    public float bombTimer = 60f;
    [Tooltip("The fuse of a sudden death round (tie for the first place).")]
    [SerializeField] private float _suddenDeathFuse = 20f;
    [Tooltip("Whoever receives the bomb always has at least this long before it explodes.")]
    [SerializeField] private float _minFuseOnReceive = 8f;
    [Tooltip("The guaranteed time shrinks by this much each time it is used, so a round always ends.")]
    [SerializeField] private float _minFuseStep = 2f;
    [Tooltip("The guaranteed time never goes below this.")]
    [SerializeField] private float _minFuseLowest = 3f;

    [Header("Pass")]

    [Tooltip("A pass on contact is refused when the two players are further apart than this on the server.")]
    [SerializeField] private float _maxPassDistance = 1.5f;

    public NetworkVariable<ulong> bombHolderId = new NetworkVariable<ulong>(NoPlayer);

    // Server time of the explosion, or NoExplosion between bombs. Sent once per bomb:
    // every peer computes the remaining time locally from the synchronized server clock.
    public NetworkVariable<double> ExplosionTime = new NetworkVariable<double>(NoExplosion);

    // Server only: this round's fuse and guaranteed time on receiving the bomb.
    private bool _isSuddenDeath;
    private float _currentMinFuse;

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
        // machine (PlayerBomb.OnCollisionEnter2D), and the sender is checked again here.
        if (!RpcValidation.SenderMatches(rpcParams, bombHolderId.Value))
            return;

        if (!AreInPassRange(bombHolderId.Value, newHolderId))
            return;

        AssignBombHolder(newHolderId);
    }

    private bool AreInPassRange(ulong holderId, ulong targetId)
    {
        PlayerBomb holder = GetBombState(holderId);
        PlayerBomb target = GetBombState(targetId);
        if (holder == null || target == null)
            return false;

        Vector3 from = holder.transform.position;
        Vector3 to = target.transform.position;
        return BombRules.IsWithinPassRange(from.x, from.y, to.x, to.y, _maxPassDistance);
    }

    // Server only. Returns true if the bomb actually changed hands.
    public bool AssignBombHolder(ulong newHolderId)
    {
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(newHolderId))
            return false;

        PlayerBomb newHolder = GetBombState(newHolderId);

        if (!CanTransferBombTo(newHolder, newHolderId))
            return false;

        if (!TryReleaseCurrentHolder())
            return false;

        // First bomb of the round: light the fuse. Afterwards, a pass: the receiver gets his
        // guaranteed time.
        if (!IsRoundRunning)
            StartCountdown();
        else
            ApplyMinimumFuse();

        GiveBombTo(newHolder, newHolderId);

        bombHolderId.Value = newHolderId;
        return true;
    }

    // Server only (GameRoundManager): the next round is a sudden death, with a shorter fuse.
    public void SetSuddenDeath(bool suddenDeath)
    {
        _isSuddenDeath = suddenDeath;
    }

    private void ApplyMinimumFuse()
    {
        double now = NetworkManager.ServerTime.Time;
        double remaining = ExplosionTime.Value - now;

        if (!BombRules.UsesMinimum(remaining, _currentMinFuse))
            return;

        ExplosionTime.Value = now + BombRules.FuseAfterPass(remaining, _currentMinFuse);
        _currentMinFuse = BombRules.NextMinimum(_currentMinFuse, _minFuseStep, _minFuseLowest);
    }

    // The bomb state of a player's character, or null (unknown player, no character).
    // Through the registry: the character is not a child of its player object.
    private static PlayerBomb GetBombState(ulong clientId)
    {
        PlayerCharacter character = Players.TryGet(clientId, out PlayerIdentity player) ? player.Character : null;
        return character != null ? character.GetComponent<PlayerBomb>() : null;
    }

    private bool CanTransferBombTo(PlayerBomb target, ulong newHolderId)
    {
        // Checked here, on the server: the client-side checks in PlayerBomb are only a
        // pre-filter, and a thrown bomb (BombProjectile) has none.
        bool isAlive = target != null && target.IsAlive.Value;
        bool isInvincible = target != null && target.IsPlayerInvincible();

        return BombRules.CanTransfer(bombHolderId.Value, newHolderId, isAlive, isInvincible);
    }

    // Returns false if the holder is stunned with the bomb in hand: a stunned player cannot
    // pass it on. A bomb already thrown (hasBomb is false) can always change hands.
    private bool TryReleaseCurrentHolder()
    {
        // No holder yet (first bomb of the round), or he left.
        PlayerBomb holderBomb = GetBombState(bombHolderId.Value);
        if (holderBomb == null)
            return true;

        PlayerCombat holderCombat = holderBomb.GetComponent<PlayerCombat>();
        if (holderCombat != null && holderCombat.IsStunned.Value && holderBomb.HasBomb.Value)
            return false;

        holderBomb.HasBomb.Value = false;
        holderBomb.OnBombTransferred();
        return true;
    }

    private void GiveBombTo(PlayerBomb newHolderBomb, ulong newHolderId)
    {
        if (newHolderBomb != null)
            newHolderBomb.HasBomb.Value = true;
        else
            Debug.LogWarning("New bomb holder not found: " + newHolderId);
    }

    private void StartCountdown()
    {
        float duration = _isSuddenDeath ? _suddenDeathFuse : bombTimer;

        _currentMinFuse = _minFuseOnReceive;
        ExplosionTime.Value = NetworkManager.ServerTime.Time + duration;
    }

    private void ExplodeBombHolder()
    {
        PlayerBomb currentHolder = GetBombState(bombHolderId.Value);

        if (currentHolder != null)
            currentHolder.MarkAsDead();
        else
            Debug.LogWarning($"[BombManager] No character found for bomb holder {bombHolderId.Value}.");

        // StopRound() clears the holder, read it first.
        ulong explodedId = bombHolderId.Value;

        StopRound();

        BombExploded?.Invoke(explodedId);
    }

    // Every player's bomb state, alive ones only.
    private static IEnumerable<PlayerBomb> AlivePlayers()
    {
        foreach (PlayerCharacter character in Players.Characters)
        {
            PlayerBomb bomb = character.GetComponent<PlayerBomb>();
            if (bomb != null && bomb.IsAlive.Value)
                yield return bomb;
        }
    }

    private void DespawnActiveBombs()
    {
        foreach (BombProjectile bombProjectile in FindObjectsByType<BombProjectile>(FindObjectsSortMode.None))
        {
            if (bombProjectile.IsSpawned)
                bombProjectile.NetworkObject.Despawn();
        }
    }

    public void HandlePlayerLeft(ulong clientId)
    {
        if (!IsRoundRunning || bombHolderId.Value != clientId)
            return;

        // The holder left with the bomb: give it to a random alive player.
        // The fuse keeps burning (AssignBombHolder only lights it when no bomb is running).
        List<PlayerBomb> candidates = AlivePlayers()
            .Where(p => p.OwnerClientId != clientId && !p.IsPlayerInvincible())
            .ToList();

        bombHolderId.Value = NoPlayer;

        if (candidates.Count > 0)
            AssignBombHolder(candidates[Random.Range(0, candidates.Count)].OwnerClientId);
    }

    // Server only. Clears the current bomb; the display and the music follow ExplosionTime.
    public void StopRound()
    {
        ExplosionTime.Value = NoExplosion;
        bombHolderId.Value = NoPlayer;

        DespawnActiveBombs();
    }

    public bool IsBombHolder(ulong clientId)
    {
        return bombHolderId.Value == clientId;
    }
}
