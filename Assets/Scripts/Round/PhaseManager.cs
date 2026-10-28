using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

// Server only: the whole game, in order, in one coroutine.
//   start delay → [ phase 1 (traps) → phase 2 (countdown, round) → scoreboard ] × rounds → end phase → lobby
// Every wait is declared below; the other classes only report when something happened
// (OnPlacementDone, OnRoundEnded, OnScoreboardClosed). The game no longer moves on at the pace
// of the host's scoreboard animation: a timeout takes over if its end event never comes.
public class PhaseManager : NetworkBehaviour
{
    public enum GamePhase
    {
        Starting,
        Phase1,
        Phase2,
        Scoreboard,
        EndPhase
    }

    [SerializeField] private Phase1 phase1;
    [SerializeField] private Phase2 phase2;
    [SerializeField] private EndPhase endGame;

    [Header("Durations (seconds)")]
    [Tooltip("After the level has loaded, before the first phase 1.")]
    [SerializeField] private float _startDelay = 3f;
    [Tooltip("Phase 2 countdown, shown second by second.")]
    [SerializeField] private int _countdownSeconds = 5;
    [Tooltip("How long \"Go!\" stays on screen before the bomb is given.")]
    [SerializeField] private float _goDisplayTime = 1f;
    [Tooltip("From the start of phase 2: players revived and traps switched on.")]
    [SerializeField] private float _trapActivationDelay = 2f;
    [Tooltip("The scores travel through NetworkVariables (next network tick): wait before showing them.")]
    [SerializeField] private float _scoreSyncDelay = 3f;
    [Tooltip("Longest wait for the scoreboard to close (its animation event) before going on anyway.")]
    [SerializeField] private float _scoreboardTimeout = 60f;
    [Tooltip("After the scoreboard has closed, before the next phase 1.")]
    [SerializeField] private float _afterScoreboardDelay = 3f;
    [Tooltip("How long \"not enough players\" stays on screen before going back to the lobby.")]
    [SerializeField] private float _notEnoughPlayersDelay = 5f;
    // How long the winner is shown: EndPhase's Display Duration (kept there, already set in the scene).

    // Replicated so any peer can tell where the game is (debug, UI).
    private readonly NetworkVariable<GamePhase> _currentPhase = new(GamePhase.Starting);
    public GamePhase CurrentPhase => _currentPhase.Value;

    private Spawner _Spawner;

    // Server only: set by the events below, consumed by the sequence.
    private bool _placementDone;
    private bool _roundEnded;
    private ulong[] _survivorIds = Array.Empty<ulong>();
    private bool _scoreboardClosed;
    private bool _isEndingEarly;

    // Also true once the session stops (quit, disconnect): the sequence must not send anything any more.
    private static bool IsGameOver =>
        GameRoundManager.Instance == null || GameRoundManager.Instance.IsGameOver
        || !IsSessionRunning;

    private static bool IsSessionRunning =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.ShutdownInProgress;

    private void Start()
    {
        if (!IsServer)
            return;

        _Spawner = FindFirstObjectByType<Spawner>();
        StartCoroutine(RunGame());
    }

    #region Events (server)

    // The game's managers only announce what happened; this sequencer listens and decides. None
    // of them calls it back (they used to, in both directions).
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer)
            return;

        GameRoundManager.Instance.RoundEnded += OnRoundEnded;
        GameRoundManager.Instance.NotEnoughPlayers += OnNotEnoughPlayers;
        GameRoundManager.Instance.Display.ScoreboardClosed += OnScoreboardClosed;
        TrapPlacementManager.Instance.PlacementDone += OnPlacementDone;
        PlayerManager.PlayerLeft += OnPlayerLeft;
    }

    public override void OnNetworkDespawn()
    {
        if (GameRoundManager.Instance != null)
        {
            GameRoundManager.Instance.RoundEnded -= OnRoundEnded;
            GameRoundManager.Instance.NotEnoughPlayers -= OnNotEnoughPlayers;
            GameRoundManager.Instance.Display.ScoreboardClosed -= OnScoreboardClosed;
        }

        if (TrapPlacementManager.Instance != null)
            TrapPlacementManager.Instance.PlacementDone -= OnPlacementDone;

        PlayerManager.PlayerLeft -= OnPlayerLeft;

        base.OnNetworkDespawn();
    }

    // PlayerManager: another player has left. The round rules first; the traps only while the
    // game goes on (a game ended for lack of players has already removed them).
    private void OnPlayerLeft(ulong clientId)
    {
        GameRoundManager.Instance.HandlePlayerLeft(clientId);

        if (!GameRoundManager.Instance.IsGameOver)
            TrapPlacementManager.Instance.HandlePlayerLeft(clientId);
    }

    // TrapPlacementManager: every trap of this phase 1 has been placed.
    private void OnPlacementDone()
    {
        _placementDone = true;
    }

    // GameRoundManager: one player or none left alive.
    private void OnRoundEnded(ulong[] survivorIds)
    {
        _survivorIds = survivorIds;
        _roundEnded = true;
    }

    // GameRoundManager: a player left and fewer than two are still here. The game ends here too,
    // instead of in a coroutine of its own: every way out of a game goes through this sequencer.
    private void OnNotEnoughPlayers()
    {
        if (_isEndingEarly)
            return;

        _isEndingEarly = true;

        // The game sequence and whatever it started (countdown, trap activation, chest).
        StopAllCoroutines();
        phase1.StopSelection();

        StartCoroutine(EndForLackOfPlayers());
    }

    // RoundDisplay: the host's scoreboard has finished its closing animation.
    private void OnScoreboardClosed()
    {
        _scoreboardClosed = true;
    }

    #endregion

    private IEnumerator RunGame()
    {
        GameRoundManager rounds = GameRoundManager.Instance;

        yield return new WaitForSeconds(_startDelay);
        if (IsGameOver)
            yield break;

        ResetCharactersPosition();

        // Left only by "break" when the game is won; every other stop ends the coroutine.
        while (true)
        {
            // Phase 1: chest, choice and placement of the traps.
            _placementDone = false;
            _currentPhase.Value = GamePhase.Phase1;
            phase1.Begin();
            yield return new WaitUntil(() => _placementDone || IsGameOver);
            if (IsGameOver)
                yield break;

            // Phase 2: countdown, then the round runs until one player is left.
            _roundEnded = false;
            _currentPhase.Value = GamePhase.Phase2;
            phase2.Begin();
            StartCoroutine(ActivateTrapsAfterDelay());
            yield return Countdown();
            if (IsGameOver)
                yield break;

            rounds.StartNewRound();
            yield return new WaitUntil(() => _roundEnded || IsGameOver);
            if (IsGameOver)
                yield break;

            // Scoreboard.
            _scoreboardClosed = false;
            _currentPhase.Value = GamePhase.Scoreboard;
            yield return new WaitForSeconds(_scoreSyncDelay);
            if (IsGameOver)
                yield break;

            rounds.Display.ShowScoreboardClientRpc(_survivorIds);
            yield return WaitOrTimeout(() => _scoreboardClosed || IsGameOver, _scoreboardTimeout, "scoreboard closing");
            yield return new WaitForSeconds(_afterScoreboardDelay);

            // A player may have left during the scoreboard and ended the game.
            if (IsGameOver)
                yield break;

            if (rounds.FinishRound())
                break;

            // At the end of phase 2, before the characters are hidden for the next phase 1.
            ResetCharactersPosition();
        }

        // Only reached when the game is won (a game ended for lack of players: EndForLackOfPlayers).
        _currentPhase.Value = GamePhase.EndPhase;
        endGame.Begin();
        yield return new WaitForSeconds(endGame.DisplayDuration);

        SceneLoader.LoadForEveryone(SceneNames.Lobby);
    }

    private IEnumerator EndForLackOfPlayers()
    {
        _currentPhase.Value = GamePhase.EndPhase;
        GameRoundManager.Instance.Display.ShowMessageClientRpc("Not enough players left, back to the lobby...");

        yield return new WaitForSeconds(_notEnoughPlayersDelay);

        if (IsSessionRunning)
            SceneLoader.LoadForEveryone(SceneNames.Lobby);
    }

    private IEnumerator Countdown()
    {
        for (int secondsLeft = _countdownSeconds; secondsLeft > 0; secondsLeft--)
        {
            if (IsGameOver)
                yield break;

            phase2.ShowCountdownClientRpc(secondsLeft.ToString());
            yield return new WaitForSeconds(1f);
        }

        if (IsGameOver)
            yield break;

        phase2.ShowCountdownClientRpc("Go!");
        yield return new WaitForSeconds(_goDisplayTime);

        if (!IsGameOver)
            phase2.ShowCountdownClientRpc("");
    }

    // Runs alongside the countdown.
    private IEnumerator ActivateTrapsAfterDelay()
    {
        yield return new WaitForSeconds(_trapActivationDelay);

        if (IsGameOver)
            yield break;

        // Revive everyone and rebuild the round's player list before the traps go live.
        GameRoundManager.Instance.ResetPlayers();
        TrapPlacementManager.Instance.ActivateTraps();
    }

    private void ResetCharactersPosition()
    {
        if (_Spawner != null)
            _Spawner.ResetCharactersPosition();
    }

    private static IEnumerator WaitOrTimeout(Func<bool> isDone, float timeout, string step)
    {
        float giveUpTime = Time.time + timeout;

        while (!isDone() && Time.time < giveUpTime)
            yield return null;

        if (!isDone())
            Debug.LogWarning($"[PhaseManager] No end event for the {step} after {timeout}s, going on.");
    }
}