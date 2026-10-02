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
            {
                PlayerTotalScores.Value[playerId] = 0;
            }

            if (!PlayerRoundScores.Value.ContainsKey(playerId))
            {
                PlayerRoundScores.Value[playerId] = new();
            }
            else
            {
                PlayerRoundScores.Value[playerId].Clear();
            }
        }
    }

    public void AddRoundScore(ulong playerId, int score, string reason)
    {
        if (!PlayerRoundScores.Value.ContainsKey(playerId))
        {
            //PlayerRoundScores.Value[playerId] = new List<(int, string)>();
            PlayerRoundScores.Value[playerId] = new();
        }

        IntStringPair roundScore = new IntStringPair(score, reason);
        PlayerRoundScores.Value[playerId].Add((roundScore));
    }

    public void AddTotalScore(ulong playerId, int score)
    {
        if (!PlayerTotalScores.Value.ContainsKey(playerId))
        {
            PlayerTotalScores.Value[playerId] = 0;
        }

        PlayerTotalScores.Value[playerId] += score;
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
        TransferManager lastAlivePlayer = null;

        int survivalDivisor = MathUtils.SafeDivisor(players.Count);

        foreach (var player in players)
        {
            if (player.IsAlive.Value)
            {
                aliveCount++;
                lastAlivePlayer = player;
                AddRoundScore(player.OwnerClientId, ROUND_POINTS / survivalDivisor, "Survived the round");
            }
        }
        if (aliveCount <= 1)
        {
            EndRound(lastAlivePlayer);
        }
        else
        {
            SelectRandomBombHolder();
        }
    }

    private void EndRound(TransferManager winner)
    {
        ResetCameraClientRpc();

        foreach (var player in players)
        {
            PlayerCharacter playerController = player.GetComponent<PlayerCharacter>();
            if (playerController != null)
            {
                playerController.DesactivateCamera();
            }
        }

        var scoreDeltas = ComputeTotalScoreDeltas(PlayerRoundScores.Value);
        foreach (var kvp in scoreDeltas)
            AddTotalScore(kvp.Key, kvp.Value);

        PlayerIds.Clear();
        foreach (var player in players)
        {
            PlayerIds.Add(player.OwnerClientId);
        }
        SwitchScoreboardStateServerRpc();
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
        Debug.Log("G - Je suis là");
        if (mainCamera != null)
        {
            Debug.Log("G- Et là");
            mainCamera.gameObject.SetActive(true);
            MainCameraController mainCameraController = mainCamera.GetComponent<MainCameraController>();
            if (mainCameraController != null)
            {
                mainCameraController.ResetCamera();
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void SwitchScoreboardStateServerRpc()
    {
        if (_Scoreboard == null)
        {
            Debug.LogError("Scoreboard reference is null! Ensure it is assigned.");
            return;
        }

        StartCoroutine(SwitchScoreboardState());
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SwitchScoreboardStateClientRpc()
    {
        Debug.Log("Switching to scoreboard state in client...");
        int idx = 0;

        foreach (ulong playerId in PlayerIds)
        {
            // Bug corrige : "NetworkManager.Singleton.ConnectedClients" n'est accessible
            // que sur le serveur (l'API Unity le documente explicitement, et leve une
            // erreur sur un client normal). Comme ce ClientRpc s'execute sur TOUS les
            // clients, cette recherche plantait systematiquement sur toute partie a
            // plusieurs machines (invisible en test solo dans l'Editeur, ou le seul
            // "client" est aussi l'hote). Remplace par une recherche via OwnerClientId,
            // qui est repliquee et donc valide sur n'importe quel pair.
            PlayerIdentity playerController = FindObjectsByType<PlayerIdentity>(FindObjectsSortMode.None)
                .FirstOrDefault(p => p.OwnerClientId == playerId);

            if (playerController == null || playerController.Characters == null)
            {
                Debug.Log("Error when trying to acces to the player instance");
                // Bug corrige : "return;" ici arretait TOUTE la methode des le premier
                // joueur manquant, empechant le scoreboard de s'afficher pour tout le
                // monde. "continue;" saute seulement ce joueur et continue les autres.
                continue;
            }

            // Get Au_Player component from the player prefab
            Sprite sprite = playerController.Characters.GetComponent<SpriteRenderer>().sprite;

            if (sprite == null)
            {
                Debug.Log("Error when trying to acces to the sprite of the player gameObject");
                continue;
            }

            _Scoreboard.PlayerImage[idx].sprite = sprite;

            idx++;
        }

        _Scoreboard.SetUpPlayerStateImages();
        _Scoreboard.UpdateDisplayScore();
        winnerText.gameObject.SetActive(false);
        _Scoreboard.gameObject.SetActive(true);
        _Scoreboard.SwitchScoreboardState();
    }

    private IEnumerator SwitchScoreboardState()
    {
        yield return new WaitForSeconds(3);

        SwitchScoreboardStateClientRpc();
    }

    public IEnumerator ChangePhase()
    {
        if (IsHost)
        {
            yield return new WaitForSeconds(3);

            //winnerText.gameObject.SetActive(false);
            DesactivateWinnerTextClientRpc();

            //Debug.Log("Chaging Phase from phase 2");


            roundCount++;
            if (roundCount >= MAX_ROUNDS || PlayerTotalScores.Value.Values.Any(score => score >= MAX_SCORE))
            {
                Debug.Log("Game over!");
                foreach (var player in FindObjectsByType<TransferManager>(FindObjectsSortMode.None))
                {
                    player.Revive();
                }
                //display the winner in the winner text
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