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
    [Serializable]
    public class IntStringPair : INetworkSerializable, IEquatable<IntStringPair>
    {
        public int IntValue;
        public string StringValue;

        public IntStringPair()
        {
            IntValue = 0;
            StringValue = "";
        }

        public IntStringPair(int intValue, string stringValue)
        {
            IntValue = intValue;
            StringValue = stringValue;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref IntValue);
            serializer.SerializeValue(ref StringValue);
        }

        public bool Equals(IntStringPair other)
        {
            if (other == null)
                return false;

            return IntValue == other.IntValue && StringValue == other.StringValue;
        }

        public override bool Equals(object obj)
        {
            return obj is IntStringPair other && Equals(other);
        }

        public override int GetHashCode()
        {
            return IntValue.GetHashCode() ^ (StringValue?.GetHashCode() ?? 0);
        }
    }

    [Serializable]
    public class CustomDictionary : INetworkSerializable, IEnumerable<KeyValuePair<ulong, List<IntStringPair>>>, IEquatable<CustomDictionary>
    {
        public Dictionary<ulong, List<IntStringPair>> Data = new Dictionary<ulong, List<IntStringPair>>();

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            int count = Data.Count;
            if (serializer.IsWriter)
            {
                serializer.SerializeValue(ref count);
                foreach (var pair in Data)
                {
                    ulong key = pair.Key;
                    serializer.SerializeValue(ref key);

                    int listCount = pair.Value.Count;
                    serializer.SerializeValue(ref listCount);
                    foreach (var item in pair.Value)
                    {
                        item.NetworkSerialize(serializer);
                    }
                }
            }
            else
            {
                serializer.SerializeValue(ref count);
                Data.Clear();
                for (int i = 0; i < count; i++)
                {
                    ulong key = 0;
                    serializer.SerializeValue(ref key);

                    int listCount = 0;
                    serializer.SerializeValue(ref listCount);

                    var list = new List<IntStringPair>();
                    for (int j = 0; j < listCount; j++)
                    {
                        var item = new IntStringPair();
                        item.NetworkSerialize(serializer);
                        list.Add(item);
                    }

                    Data[key] = list;
                }
            }
        }

        // Wrapper for ContainsKey
        public bool ContainsKey(ulong key)
        {
            return Data.ContainsKey(key);
        }

        // Wrapper for Add
        public void Add(ulong key, List<IntStringPair> value)
        {
            Data.Add(key, value);
        }

        // Wrapper for Remove
        public bool Remove(ulong key)
        {
            return Data.Remove(key);
        }

        // Wrapper for TryGetValue
        public bool TryGetValue(ulong key, out List<IntStringPair> value)
        {
            return Data.TryGetValue(key, out value);
        }

        // Indexer for accessing elements
        public List<IntStringPair> this[ulong key]
        {
            get => Data[key]; // Throws KeyNotFoundException if the key doesn't exist
            set => Data[key] = value;
        }

        // Implement IEnumerable
        public IEnumerator<KeyValuePair<ulong, List<IntStringPair>>> GetEnumerator()
        {
            return Data.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // Implement IEquatable<CustomDictionary>
        public bool Equals(CustomDictionary other)
        {
            if (other == null)
                return false;

            if (Data.Count != other.Data.Count)
                return false;

            foreach (var kvp in Data)
            {
                if (!other.Data.TryGetValue(kvp.Key, out var otherValue))
                    return false;

                if (kvp.Value.Count != otherValue.Count)
                    return false;

                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    if (!kvp.Value[i].Equals(otherValue[i]))
                        return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is CustomDictionary other && Equals(other);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            foreach (var kvp in Data)
            {
                hash = hash * 31 + kvp.Key.GetHashCode();
                foreach (var pair in kvp.Value)
                {
                    hash = hash * 31 + pair.GetHashCode();
                }
            }
            return hash;
        }
    }

    public List<TransferManager> players = new();

    public NetworkVariable<Dictionary<ulong, int>> PlayerTotalScores { get; private set; }
    public NetworkVariable<CustomDictionary> PlayerRoundScores = new();
    public NetworkList<ulong> PlayerIds = new NetworkList<ulong>();

    // TEMPORARY: This is a temporary fix to stop the game after 5 rounds
    private int roundCount = 0;
    private const int MAX_ROUNDS = 25;
    private const int MAX_SCORE = 90;
    private const int ROUND_POINTS = 18;
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

    public void StartNewRound()
    {
        ResetPlayers();
        SelectRandomBombHolder();
    }

    public void ResetPlayers()
    {
        players.Clear();
        foreach (var player in FindObjectsByType<TransferManager>(FindObjectsSortMode.None))
        {
            player.Revive();
            players.Add(player);
            ulong playerId = player.OwnerClientId;

            if (!PlayerTotalScores.Value.ContainsKey(playerId))
                PlayerTotalScores.Value[playerId] = 0;

            if (!PlayerRoundScores.Value.ContainsKey(playerId))
                PlayerRoundScores.Value[playerId] = new();
            else
                PlayerRoundScores.Value[playerId].Clear();
        }

        // Drop the round scores of players who left. Safe here: the scoreboard is closed.
        foreach (ulong id in PlayerRoundScores.Value.Data.Keys.ToList())
        {
            if (!players.Exists(p => p.OwnerClientId == id))
                PlayerRoundScores.Value.Remove(id);
        }

        MarkScoresDirty();
    }

    public void AddRoundScore(ulong playerId, int score, string reason)
    {
        if (!PlayerRoundScores.Value.ContainsKey(playerId))
            PlayerRoundScores.Value[playerId] = new();

        PlayerRoundScores.Value[playerId].Add(new IntStringPair(score, reason));
        MarkScoresDirty();
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
        List<TransferManager> alivePlayers = new List<TransferManager>();
        foreach (var player in players)
        {
            if (player.IsAlive.Value)
            {
                alivePlayers.Add(player);
            }
        }

        if (alivePlayers.Count > 1)
        {
            BombManager.Instance.ResetBombServerRpc();
            int randomIndex = UnityEngine.Random.Range(0, alivePlayers.Count);
            ulong randomPlayerId = alivePlayers[randomIndex].OwnerClientId;
            BombManager.Instance.AssignBombHolder(randomPlayerId);
        }
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
                AddRoundScore(player.OwnerClientId, ROUND_POINTS / survivalDivisor, SURVIVAL_REASON);
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

        foreach (var player in players)
        {
            PlayerCharacter playerController = player.GetComponent<PlayerCharacter>();
            if (playerController != null)
                playerController.DesactivateCamera();
        }

        var scoreDeltas = ComputeTotalScoreDeltas(PlayerRoundScores.Value);
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
        ulong[] survivorIds = players.Where(p => p.IsAlive.Value).Select(p => p.OwnerClientId).ToArray();
        StartCoroutine(ShowScoreboard(survivorIds));
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

    private void EndGameForLackOfPlayers()
    {
        _isGameOver = true;

        BombManager.Instance.StopRound();

        foreach (var player in FindObjectsByType<TransferManager>(FindObjectsSortMode.None))
            player.Revive();

        TrapPlacementManager.Instance.RemoveTrapServerRpc();
        ShowGameMessageClientRpc("Not enough players left, back to the lobby...");
        StartCoroutine(GoBackToLobby());
    }

    // NGO already detects in-place changes by comparing these collections with a copy on every
    // network tick. Calling it explicitly documents that they are replicated to the clients.
    private void MarkScoresDirty()
    {
        PlayerTotalScores.CheckDirtyState();
        PlayerRoundScores.CheckDirtyState();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowGameMessageClientRpc(string message)
    {
        winnerText.gameObject.SetActive(true);
        winnerText.text = message;
    }

    public static Dictionary<ulong, int> ComputeTotalScoreDeltas(CustomDictionary roundScores)
    {
        var deltas = new Dictionary<ulong, int>();

        foreach (var player in roundScores)
        {
            int sum = 0;
            foreach (var score in player.Value)
                sum += score.IntValue;

            deltas[player.Key] = sum;
        }

        return deltas;
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void ResetCameraClientRpc()
    {
        if (mainCamera != null)
        {
            mainCamera.gameObject.SetActive(true);
            MainCameraController mainCameraController = mainCamera.GetComponent<MainCameraController>();
            if (mainCameraController != null)
            {
                mainCameraController.ResetCamera();
            }
        }
    }

    private IEnumerator ShowScoreboard(ulong[] survivorIds)
    {
        // Do not shorten carelessly: the scores travel through NetworkVariables (sent on the next
        // network tick), not through this RPC. The delay guarantees every client has the final
        // scores before the scoreboard reads them.
        yield return new WaitForSeconds(3);

        ShowScoreboardClientRpc(survivorIds);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowScoreboardClientRpc(ulong[] survivorIds)
    {
        PlayerIdentity[] identities = FindObjectsByType<PlayerIdentity>(FindObjectsSortMode.None);

        for (int i = 0; i < PlayerIds.Count; i++)
        {
            PlayerIdentity identity = identities.FirstOrDefault(p => p.OwnerClientId == PlayerIds[i]);

            if (identity == null || identity.Characters == null)
            {
                Debug.LogWarning($"[Scoreboard] No character found for player {PlayerIds[i]}.");
                continue;
            }

            Sprite sprite = identity.Characters.GetComponent<SpriteRenderer>().sprite;

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

    public IEnumerator ChangePhase()
    {
        if (!IsHost || _isGameOver)
            yield break;

        yield return new WaitForSeconds(3);

        // A player may have left during the wait and ended the game.
        if (_isGameOver)
            yield break;

        DesactivateWinnerTextClientRpc();

        roundCount++;
        if (roundCount >= MAX_ROUNDS || PlayerTotalScores.Value.Values.Any(score => score >= MAX_SCORE))
        {
            _isGameOver = true;
            Debug.Log("Game over!");

            foreach (var player in FindObjectsByType<TransferManager>(FindObjectsSortMode.None))
                player.Revive();

            ulong winnerUlong = PlayerTotalScores.Value.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
            winnerText.text = $"Player {winnerUlong} is the real King of Pirates!";
            TrapPlacementManager.Instance.RemoveTrapServerRpc();
            StartCoroutine(GoBackToLobby());
        }
        else
        {
            _PhaseManager.SwitchGamePhaseServerRpc();
        }
    }

    private IEnumerator GoBackToLobby()
    {
        yield return new WaitForSeconds(5);
        NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void NotifyClientsOfWinnerClientRpc(ulong winnerId)
    {
        // Eventuellement d�clencher des effets visuels ou audio
        winnerText.gameObject.SetActive(true);
        winnerText.text = $"Player {winnerId} wins the round!";
        Debug.Log("Winner: " + winnerText.text);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void DesactivateWinnerTextClientRpc()
    {
        winnerText.gameObject.SetActive(false);
    }
}