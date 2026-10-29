using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

// The rules of a round, on the server: who plays it, who gets the bomb, the gold it costs (bomb,
// traps), and what happens when a player leaves. Gold is ScoreKeeper's, what players see is
// RoundDisplay's; PhaseManager runs the game and listens to the events below.
// One bomb per round: its explosion ends the round. The game ends when someone has no gold left;
// a tie for the first place is played again as a sudden death between the tied players.
[RequireComponent(typeof(ScoreKeeper), typeof(RoundDisplay))]
public class GameRoundManager : NetworkSingleton<GameRoundManager>
{
    private const string BlownUpReason = "Blown up";
    private const string TrappedReason = "Caught by traps";
    private const string TrapBonusReason = "Trap bonus";

    [Header("Gold")]
    [Tooltip("Gold lost by the player the bomb explodes on. Lost for good: nobody wins it.")]
    [SerializeField] private int _bombLoss = 25;
    [Tooltip("Gold lost when the last trap life goes; the trap's owner wins it (nobody does on one's own trap).")]
    [SerializeField] private int _trapLoss = 10;

    [Header("Trap lives")]
    [SerializeField] private int _livesPerRound = 3;
    [Tooltip("After a respawn, trap hits are ignored for this long (no loop on a trap near the spawn).")]
    [SerializeField] private float _respawnProtection = 1.5f;
    [Tooltip("Two trap hits closer than this count once (the same contact reported twice).")]
    [SerializeField] private float _hitCooldown = 0.5f;

    // Server only. Listened to by PhaseManager, which runs the game: this manager never calls it.
    public event Action<ulong[]> RoundEnded;        // the survivors
    public event Action NotEnoughPlayers;

    public ScoreKeeper Scores { get; private set; }
    public RoundDisplay Display { get; private set; }

    public bool IsGameOver { get; private set; }

    // Server only: the coming round is a sudden death between the players tied for the first place.
    public bool IsSuddenDeath { get; private set; }

    // Server only: the bomb state of every player in the current round.
    private readonly List<PlayerBomb> _players = new();
    private readonly HashSet<ulong> _suddenDeathPlayers = new();

    // Server only: per player, the time before which trap hits are ignored.
    private readonly Dictionary<ulong, float> _ignoreTrapHitsUntil = new();

    private Spawner _spawner;

    protected override void OnSingletonAwake()
    {
        Scores = GetComponent<ScoreKeeper>();
        Display = GetComponent<RoundDisplay>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
            return;

        _spawner = FindFirstObjectByType<Spawner>();

        if (BombManager.Instance != null)
            BombManager.Instance.BombExploded += OnBombExploded;

        PlayerCombat.HitByTrapOnServer += OnHitByTrap;
    }

    public override void OnNetworkDespawn()
    {
        if (BombManager.Instance != null)
            BombManager.Instance.BombExploded -= OnBombExploded;

        // Static: unsubscribed in any case, or the next game would call into this destroyed manager.
        PlayerCombat.HitByTrapOnServer -= OnHitByTrap;

        base.OnNetworkDespawn();
    }

    #region Round (server)

    // Early in phase 2 (PhaseManager): the round's players revived with full trap lives. In a sudden
    // death, the others watch, dead (they can neither get the bomb nor be hit).
    public void ResetPlayers()
    {
        _players.Clear();
        _ignoreTrapHitsUntil.Clear();

        foreach (PlayerCharacter character in Players.Characters)
        {
            PlayerBomb player = character.GetComponent<PlayerBomb>();

            if (IsSuddenDeath && !_suddenDeathPlayers.Contains(player.OwnerClientId))
            {
                player.MarkAsDead();
                continue;
            }

            player.Revive();
            character.GetComponent<PlayerCombat>().TrapLives.Value = _livesPerRound;
            _players.Add(player);
        }

        Scores.BeginRound(AllPlayerIds());
    }

    // The players are already listed (ResetPlayers): starting a round only needs a bomb holder.
    public void StartNewRound()
    {
        BombManager.Instance.SetSuddenDeath(IsSuddenDeath);

        // Fewer than two players able to play (a sudden death partner left): nothing to play, the
        // round ends at once and FinishRound decides with the gold as it is.
        if (!SelectRandomBombHolder())
            EndRound();
    }

    // After the scoreboard (PhaseManager). Returns true when the game is over. A tie for the first
    // place, once someone is broke, makes the next round a sudden death between the tied players.
    public bool FinishRound()
    {
        Display.HideMessageClientRpc();

        IsSuddenDeath = false;
        _suddenDeathPlayers.Clear();

        if (!Scores.IsSomeoneBroke())
            return false;

        ulong[] leaders = Scores.GetWinnerIds();
        if (leaders.Length > 1)
        {
            IsSuddenDeath = true;
            _suddenDeathPlayers.UnionWith(leaders);
            return false;
        }

        IsGameOver = true;
        return true;
    }

    // One bomb per round: its explosion takes the holder's gold and ends the round.
    private void OnBombExploded(ulong explodedId)
    {
        if (explodedId != BombManager.NoPlayer)
            Scores.Take(explodedId, _bombLoss, BlownUpReason);

        EndRound();
    }

    // Returns false when no player could receive the bomb.
    private bool SelectRandomBombHolder()
    {
        List<PlayerBomb> alivePlayers = _players.Where(p => p != null && p.IsAlive.Value).ToList();

        if (alivePlayers.Count < 2)
            return false;

        // Try the players in a random order: one may refuse the bomb (still invincible).
        foreach (PlayerBomb candidate in alivePlayers.OrderBy(_ => UnityEngine.Random.value))
        {
            if (BombManager.Instance.AssignBombHolder(candidate.OwnerClientId))
                return true;
        }

        Debug.LogWarning("[GameRoundManager] No alive player could receive the bomb.");
        return false;
    }

    private void EndRound()
    {
        Display.ResetCameraClientRpc();
        Scores.CloseRound(AllPlayerIds());

        // Sent with the scoreboard RPC instead of being guessed from the score reasons on the
        // clients. PhaseManager shows the scoreboard once the scores have reached the clients.
        ulong[] survivorIds = _players.Where(p => p != null && p.IsAlive.Value).Select(p => p.OwnerClientId).ToArray();
        RoundEnded?.Invoke(survivorIds);
    }

    // Every player of the game, sudden death spectators included (scoreboard columns, gold).
    private static List<ulong> AllPlayerIds()
    {
        return Players.Characters.Select(c => c.OwnerClientId).ToList();
    }

    #endregion

    #region Trap lives (server)

    // PlayerCombat: a trap hit a character. Each hit takes a life; the last one costs gold, which
    // the trap's owner wins, and sends the character back to a spawn point with all its lives.
    private void OnHitByTrap(PlayerCombat victim, ulong trapOwnerId)
    {
        if (IsGameOver || BombManager.Instance == null || !BombManager.Instance.IsRoundRunning)
            return;

        ulong victimId = victim.OwnerClientId;
        if (!_players.Exists(p => p != null && p.OwnerClientId == victimId))
            return;

        float now = Time.time;
        if (_ignoreTrapHitsUntil.TryGetValue(victimId, out float ignoreUntil) && now < ignoreUntil)
            return;

        _ignoreTrapHitsUntil[victimId] = now + _hitCooldown;

        victim.TrapLives.Value = Mathf.Max(0, victim.TrapLives.Value - 1);
        if (victim.TrapLives.Value > 0)
            return;

        int taken = Scores.Take(victimId, _trapLoss, TrappedReason);

        // Nobody wins on one's own trap, nor a player who has left.
        if (trapOwnerId != victimId && Scores.PlayerTotalScores.Value.ContainsKey(trapOwnerId))
            Scores.Give(trapOwnerId, taken, TrapBonusReason);

        Respawn(victim);
    }

    // Back on a spawn point with full lives, on its feet. A bomb holder keeps the bomb.
    private void Respawn(PlayerCombat victim)
    {
        victim.TrapLives.Value = _livesPerRound;
        victim.ResetState();
        _ignoreTrapHitsUntil[victim.OwnerClientId] = Time.time + _respawnProtection;

        if (_spawner != null)
            _spawner.Respawn(victim.GetComponent<PlayerCharacter>());
    }

    #endregion

    #region Players leaving (server, called by PhaseManager)

    public void HandlePlayerLeft(ulong clientId)
    {
        if (!IsServer || IsGameOver)
            return;

        // The leaving character was despawned in PlayerIdentity.OnNetworkDespawn.
        _players.RemoveAll(p => p == null || !p.IsSpawned);
        _suddenDeathPlayers.Remove(clientId);
        Scores.RemovePlayer(clientId);

        if (NetworkManager.Singleton.ConnectedClientsList.Count < 2)
        {
            EndGameForLackOfPlayers();
            return;
        }

        if (!BombManager.Instance.IsRoundRunning)
            return;

        // Fewer than two players left in the round: it ends without an explosion.
        if (_players.Count(p => p.IsAlive.Value) <= 1)
        {
            BombManager.Instance.StopRound();
            EndRound();
        }
        else
        {
            BombManager.Instance.HandlePlayerLeft(clientId);
        }
    }

    private void EndGameForLackOfPlayers()
    {
        IsGameOver = true;

        BombManager.Instance.StopRound();

        foreach (PlayerCharacter character in Players.Characters)
            character.GetComponent<PlayerBomb>().Revive();

        TrapPlacementManager.Instance.DespawnAllTraps();

        // The message and the return to the lobby belong to the sequencer.
        NotEnoughPlayers?.Invoke();
    }

    #endregion
}
