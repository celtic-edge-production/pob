using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

// The rules of a round, on the server: who plays it, who gets the bomb, when it ends, and what
// happens when a player leaves. Scores are ScoreKeeper's, what players see is RoundDisplay's;
// PhaseManager runs the game and listens to the events below.
[RequireComponent(typeof(ScoreKeeper), typeof(RoundDisplay))]
public class GameRoundManager : NetworkSingleton<GameRoundManager>
{
    private const string SurvivalReason = "Survived an explosion";
    private const string AssistReason = "Assist";
    private const int AssistPoints = 2;

    [Header("Game Rules")]
    [Tooltip("Points given to each survivor of an explosion, divided by (players - 1).")]
    [SerializeField] private int _roundPoints = 18;

    // Server only. Listened to by PhaseManager, which runs the game: this manager never calls it.
    public event Action<ulong[]> RoundEnded;        // the survivors
    public event Action NotEnoughPlayers;

    public ScoreKeeper Scores { get; private set; }
    public RoundDisplay Display { get; private set; }

    public bool IsGameOver { get; private set; }

    // Server only: the bomb state of every player in the current round.
    private readonly List<PlayerBomb> _players = new();

    protected override void OnSingletonAwake()
    {
        Scores = GetComponent<ScoreKeeper>();
        Display = GetComponent<RoundDisplay>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer && BombManager.Instance != null)
            BombManager.Instance.BombExploded += OnBombExploded;
    }

    public override void OnNetworkDespawn()
    {
        if (BombManager.Instance != null)
            BombManager.Instance.BombExploded -= OnBombExploded;

        base.OnNetworkDespawn();
    }

    #region Round (server)

    // Early in phase 2 (PhaseManager): everyone revived and listed for the coming round.
    public void ResetPlayers()
    {
        _players.Clear();

        foreach (PlayerCharacter character in Players.Characters)
        {
            PlayerBomb player = character.GetComponent<PlayerBomb>();
            player.Revive();
            _players.Add(player);
        }

        Scores.BeginRound(_players.Select(p => p.OwnerClientId));
    }

    // The players are already listed (ResetPlayers): starting a round only needs a bomb holder.
    public void StartNewRound()
    {
        SelectRandomBombHolder();
    }

    // After the scoreboard (PhaseManager). Returns true when the game is won.
    public bool FinishRound()
    {
        Display.HideMessageClientRpc();

        if (Scores.CountRoundAndCheckWin())
            IsGameOver = true;

        return IsGameOver;
    }

    // The holder has exploded. The giver of the last pass scores an assist.
    private void OnBombExploded(ulong assistId)
    {
        if (assistId != BombManager.NoPlayer)
            Scores.AddLine(assistId, AssistPoints, AssistReason);

        CheckForRoundEnd();
    }

    private void SelectRandomBombHolder()
    {
        List<PlayerBomb> alivePlayers = _players.Where(p => p.IsAlive.Value).ToList();

        if (alivePlayers.Count < 2)
            return;

        // The previous bomb was already cleared by BombManager.StopRound(). Try the players in a
        // random order: the last giver may still be invincible and refuse the bomb, which used to
        // leave the round without a bomb (and without an end).
        foreach (PlayerBomb candidate in alivePlayers.OrderBy(_ => UnityEngine.Random.value))
        {
            if (BombManager.Instance.AssignBombHolder(candidate.OwnerClientId))
                return;
        }

        Debug.LogWarning("[GameRoundManager] No alive player could receive the bomb.");
    }

    // After every explosion: survivors score, then either the next bomb or the end of the round.
    private void CheckForRoundEnd()
    {
        int survivalPoints = _roundPoints / MathUtils.SafeDivisor(_players.Count);
        List<PlayerBomb> survivors = _players.Where(p => p.IsAlive.Value).ToList();

        foreach (PlayerBomb survivor in survivors)
            Scores.AddLine(survivor.OwnerClientId, survivalPoints, SurvivalReason);

        if (survivors.Count <= 1)
            EndRound();
        else
            SelectRandomBombHolder();
    }

    private void EndRound()
    {
        Display.ResetCameraClientRpc();
        Scores.CloseRound(_players.Select(p => p.OwnerClientId).ToList());

        // Sent with the scoreboard RPC instead of being guessed from the score reasons on the
        // clients. PhaseManager shows the scoreboard once the scores have reached the clients.
        ulong[] survivorIds = _players.Where(p => p.IsAlive.Value).Select(p => p.OwnerClientId).ToArray();
        RoundEnded?.Invoke(survivorIds);
    }

    #endregion

    #region Players leaving (server, called by PhaseManager)

    public void HandlePlayerLeft(ulong clientId)
    {
        if (!IsServer || IsGameOver)
            return;

        // The leaving character was despawned in PlayerIdentity.OnNetworkDespawn.
        _players.RemoveAll(p => p == null || !p.IsSpawned);
        Scores.RemovePlayer(clientId);

        if (NetworkManager.Singleton.ConnectedClientsList.Count < 2)
        {
            EndGameForLackOfPlayers();
            return;
        }

        if (!BombManager.Instance.IsRoundRunning)
            return;

        if (_players.Count(p => p.IsAlive.Value) <= 1)
        {
            BombManager.Instance.StopRound();
            CheckForRoundEnd();
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
