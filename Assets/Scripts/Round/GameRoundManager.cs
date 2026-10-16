using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;
using System.Linq;
using System;

public class GameRoundManager : NetworkSingleton<GameRoundManager>
{
    public List<TransferManager> players = new();

    public NetworkVariable<Dictionary<ulong, int>> PlayerTotalScores { get; private set; }

    // This round's scoreboard lines, every player mixed, in scoring order. NGO replicates the changes.
    public NetworkList<ScoreLine> ScoreLines = new();
    public NetworkList<ulong> PlayerIds = new NetworkList<ulong>();

    private int roundCount = 0;

    [Header("Game Rules")]
    [SerializeField] private int _maxRounds = 25;
    [SerializeField] private int _maxScore = 90;
    [Tooltip("Points given to each survivor of an explosion, divided by (players - 1).")]
    [SerializeField] private int _roundPoints = 18;
    private Camera mainCamera;
    private bool _isGameOver = false;
    public bool IsGameOver => _isGameOver;
    private const string SURVIVAL_REASON = "Survived an explosion";

    [SerializeField] PhaseManager _PhaseManager;

    [SerializeField] private TMP_Text winnerText;

    [SerializeField] private Scoreboard _Scoreboard;

    protected override void OnSingletonAwake()
    {
        mainCamera = Camera.main;
        PlayerTotalScores = new NetworkVariable<Dictionary<ulong, int>>(new Dictionary<ulong, int>());
    }

    // Players were already revived and listed by ResetPlayers() early in phase 2
    // (Phase2.ActivateTraps), so starting a round only needs a bomb holder.
    public void StartNewRound()
    {
        SelectRandomBombHolder();
    }

    public void ResetPlayers()
    {
        players.Clear();

        // A new round starts with no lines (players who left included).
        ScoreLines.Clear();

        foreach (PlayerCharacter character in Players.Characters)
        {
            TransferManager player = character.GetComponent<TransferManager>();
            player.Revive();
            players.Add(player);
            ulong playerId = player.OwnerClientId;

            if (!PlayerTotalScores.Value.ContainsKey(playerId))
                PlayerTotalScores.Value[playerId] = 0;
        }

        MarkScoresDirty();
    }

    public void AddRoundScore(ulong playerId, int score, string reason)
    {
        ScoreLines.Add(new ScoreLine(playerId, score, reason));
    }

    public void AddTotalScore(ulong playerId, int score)
    {
        if (!PlayerTotalScores.Value.ContainsKey(playerId))
            PlayerTotalScores.Value[playerId] = 0;

        PlayerTotalScores.Value[playerId] += score;
        MarkScoresDirty();
    }

    private void SelectRandomBombHolder()
    {
        List<TransferManager> alivePlayers = players.Where(p => p.IsAlive.Value).ToList();

        if (alivePlayers.Count < 2)
            return;

        // The previous bomb was already cleared by BombManager.StopRound(). Try the players in a
        // random order: the last giver may still be invincible and refuse the bomb, which used to
        // leave the round without a bomb (and without an end).
        foreach (TransferManager candidate in alivePlayers.OrderBy(_ => UnityEngine.Random.value))
        {
            if (BombManager.Instance.AssignBombHolder(candidate.OwnerClientId))
                return;
        }

        Debug.LogWarning("[GameRoundManager] No alive player could receive the bomb.");
    }

    public void CheckForRoundEnd()
    {
        int aliveCount = 0;
        int survivalDivisor = MathUtils.SafeDivisor(players.Count);

        // Survivors score after every explosion, not only at the end of the round.
        foreach (var player in players)
        {
            if (player.IsAlive.Value)
            {
                aliveCount++;
                AddRoundScore(player.OwnerClientId, _roundPoints / survivalDivisor, SURVIVAL_REASON);
            }
        }

        if (aliveCount <= 1)
            EndRound();
        else
            SelectRandomBombHolder();
    }

    private void EndRound()
    {
        ResetCameraClientRpc();

        Dictionary<ulong, int> scoreDeltas = ScoreTally.SumByPlayer(CopyScoreLines());
        foreach (var kvp in scoreDeltas)
        {
            // Skip players who left during the round.
            if (players.Exists(p => p.OwnerClientId == kvp.Key))
                AddTotalScore(kvp.Key, kvp.Value);
        }

        PlayerIds.Clear();
        foreach (var player in players)
            PlayerIds.Add(player.OwnerClientId);

        if (_Scoreboard == null)
        {
            Debug.LogError("Scoreboard reference is null! Ensure it is assigned.");
            return;
        }

        // Sent with the RPC instead of being guessed from the score reasons on the clients.
        // PhaseManager shows the scoreboard once the scores have reached the clients.
        ulong[] survivorIds = players.Where(p => p.IsAlive.Value).Select(p => p.OwnerClientId).ToArray();
        _PhaseManager.OnRoundEnded(survivorIds);
    }

    public void HandlePlayerLeft(ulong clientId)
    {
        if (!IsServer || _isGameOver)
            return;

        // The leaving character was despawned in PlayerIdentity.OnNetworkDespawn.
        players.RemoveAll(p => p == null || !p.IsSpawned);
        PlayerTotalScores.Value.Remove(clientId);
        MarkScoresDirty();

        if (NetworkManager.Singleton.ConnectedClientsList.Count < 2)
        {
            EndGameForLackOfPlayers();
            return;
        }

        if (!BombManager.Instance.IsRoundRunning)
            return;

        if (players.Count(p => p.IsAlive.Value) <= 1)
        {
            BombManager.Instance.StopRound();
            CheckForRoundEnd();
        }
        else
        {
            BombManager.Instance.HandlePlayerLeft(clientId);
        }
    }

    // Every player sharing the best total wins (ties are possible).
    public static ulong[] ComputeWinnerIds(IReadOnlyDictionary<ulong, int> totals)
    {
        if (totals.Count == 0)
            return Array.Empty<ulong>();

        int best = totals.Values.Max();
        return totals.Where(kv => kv.Value == best).Select(kv => kv.Key).OrderBy(id => id).ToArray();
    }

    public ulong[] GetWinnerIds() => ComputeWinnerIds(PlayerTotalScores.Value);

    private void EndGameForLackOfPlayers()
    {
        _isGameOver = true;

        BombManager.Instance.StopRound();

        foreach (PlayerCharacter character in Players.Characters)
            character.GetComponent<TransferManager>().Revive();

        TrapPlacementManager.Instance.DespawnAllTraps();
        ShowGameMessageClientRpc("Not enough players left, back to the lobby...");
        StartCoroutine(ReturnToLobbyAfter(5.0f));
    }

    // NGO already detects in-place changes by comparing the dictionary with a copy on every
    // network tick. Calling it explicitly documents that it is replicated to the clients.
    private void MarkScoresDirty()
    {
        PlayerTotalScores.CheckDirtyState();
    }

    // Every peer: one player's lines of this round, in scoring order.
    public List<ScoreLine> GetScoreLines(ulong playerId)
    {
        return ScoreTally.ForPlayer(CopyScoreLines(), playerId);
    }

    // NetworkList can be walked with foreach but is not an IEnumerable<T>: copied for ScoreTally.
    private List<ScoreLine> CopyScoreLines()
    {
        List<ScoreLine> lines = new(ScoreLines.Count);
        foreach (ScoreLine line in ScoreLines)
            lines.Add(line);
        return lines;
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void ShowGameMessageClientRpc(string message)
    {
        winnerText.gameObject.SetActive(true);
        winnerText.text = message;
    }

    // Every peer: back to the shared main camera, and off with this peer's own character camera.
    // Only the owner can switch it off (PlayerCharacter.DesactivateCamera checks IsOwner): done on
    // the server, it only reached the host's character, and every client kept two AudioListeners.
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void ResetCameraClientRpc()
    {
        if (mainCamera != null)
        {
            mainCamera.gameObject.SetActive(true);
            MainCameraController mainCameraController = mainCamera.GetComponent<MainCameraController>();
            if (mainCameraController != null)
                mainCameraController.ResetCamera();
        }

        // After the main camera is back: DesactivateCamera never leaves a peer without a camera.
        PlayerCharacter localCharacter = Players.Local != null ? Players.Local.Character : null;
        if (localCharacter != null)
            localCharacter.DesactivateCamera();
    }

    // Called by PhaseManager after a delay: the scores travel through NetworkVariables (sent on the
    // next network tick), not through this RPC, and must reach every client first.
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void ShowScoreboardClientRpc(ulong[] survivorIds)
    {
        for (int i = 0; i < PlayerIds.Count; i++)
        {
            PlayerCharacter character = Players.TryGet(PlayerIds[i], out PlayerIdentity player) ? player.Character : null;

            if (character == null)
            {
                Debug.LogWarning($"[Scoreboard] No character found for player {PlayerIds[i]}.");
                continue;
            }

            Sprite sprite = character.GetComponent<SpriteRenderer>().sprite;

            // Same index as PlayerIds, so the image stays aligned with the score and status columns.
            if (sprite != null)
                _Scoreboard.PlayerImage[i].sprite = sprite;
        }

        _Scoreboard.SetUpPlayerStateImages(survivorIds);
        _Scoreboard.UpdateDisplayScore();
        winnerText.gameObject.SetActive(false);
        _Scoreboard.gameObject.SetActive(true);
        _Scoreboard.SwitchScoreboardState();
    }

    // Every peer, when its scoreboard has closed: only the host's tells PhaseManager.
    public void OnScoreboardClosed()
    {
        if (IsServer)
            _PhaseManager.OnScoreboardClosed();
    }

    // Server only (PhaseManager), after the scoreboard. Returns true when the game is won.
    public bool FinishRound()
    {
        DesactivateWinnerTextClientRpc();

        roundCount++;
        if (IsGameWon(roundCount, _maxRounds, PlayerTotalScores.Value.Values, _maxScore))
            _isGameOver = true;

        return _isGameOver;
    }

    // Enough rounds played, or someone reached the target score.
    public static bool IsGameWon(int roundsPlayed, int maxRounds, IEnumerable<int> totalScores, int maxScore)
    {
        return roundsPlayed >= maxRounds || totalScores.Any(score => score >= maxScore);
    }

    // Server only.
    public void LoadLobby()
    {
        NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
    }

    private IEnumerator ReturnToLobbyAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        LoadLobby();
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void DesactivateWinnerTextClientRpc()
    {
        winnerText.gameObject.SetActive(false);
    }
}