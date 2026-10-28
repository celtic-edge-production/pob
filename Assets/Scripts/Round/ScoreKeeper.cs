using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

// The scores of a game: this round's lines, every player's total, the rounds played and when the
// game is won. The server writes, every peer reads (Scoreboard). Lives next to GameRoundManager.
public class ScoreKeeper : NetworkBehaviour
{
    [Header("End of the game")]
    [SerializeField] private int _maxRounds = 25;
    [SerializeField] private int _maxScore = 90;

    public NetworkVariable<Dictionary<ulong, int>> PlayerTotalScores { get; private set; }

    // This round's lines, every player mixed, in scoring order. NGO replicates the changes.
    public NetworkList<ScoreLine> ScoreLines = new();

    // The players shown on the scoreboard, in column order (the players of the last round).
    public NetworkList<ulong> PlayerIds = new();

    // Server only.
    private int _roundsPlayed;

    private void Awake()
    {
        PlayerTotalScores = new NetworkVariable<Dictionary<ulong, int>>(new Dictionary<ulong, int>());
    }

    #region Server

    // A new round with these players: no lines yet, a total of 0 for a newcomer.
    public void BeginRound(IEnumerable<ulong> playerIds)
    {
        ScoreLines.Clear();

        foreach (ulong playerId in playerIds)
        {
            if (!PlayerTotalScores.Value.ContainsKey(playerId))
                PlayerTotalScores.Value[playerId] = 0;
        }

        MarkTotalsDirty();
    }

    public void AddLine(ulong playerId, int points, string reason)
    {
        ScoreLines.Add(new ScoreLine(playerId, points, reason));
    }

    // The round is over: its lines go into the totals of the players still here, who also become
    // the scoreboard's columns. Lines of a player who left are dropped.
    public void CloseRound(IReadOnlyCollection<ulong> playersStillHere)
    {
        foreach (KeyValuePair<ulong, int> delta in ScoreTally.SumByPlayer(CopyLines()))
        {
            if (!playersStillHere.Contains(delta.Key))
                continue;

            PlayerTotalScores.Value.TryGetValue(delta.Key, out int total);
            PlayerTotalScores.Value[delta.Key] = total + delta.Value;
        }

        PlayerIds.Clear();
        foreach (ulong playerId in playersStillHere)
            PlayerIds.Add(playerId);

        MarkTotalsDirty();
    }

    public void RemovePlayer(ulong playerId)
    {
        PlayerTotalScores.Value.Remove(playerId);
        MarkTotalsDirty();
    }

    // One more round played. True when the game is won.
    public bool CountRoundAndCheckWin()
    {
        _roundsPlayed++;
        return ScoreRules.IsGameWon(_roundsPlayed, _maxRounds, PlayerTotalScores.Value.Values, _maxScore);
    }

    public ulong[] GetWinnerIds() => ScoreRules.ComputeWinnerIds(PlayerTotalScores.Value);

    // NGO already detects in-place changes by comparing the dictionary with a copy on every
    // network tick. Calling it explicitly documents that it is replicated to the clients.
    private void MarkTotalsDirty()
    {
        PlayerTotalScores.CheckDirtyState();
    }

    #endregion

    #region Every peer

    public int TotalOf(ulong playerId)
    {
        return PlayerTotalScores.Value.TryGetValue(playerId, out int total) ? total : 0;
    }

    // One player's lines of this round, in scoring order.
    public List<ScoreLine> GetLinesOf(ulong playerId)
    {
        return ScoreTally.ForPlayer(CopyLines(), playerId);
    }

    // NetworkList can be walked with foreach but is not an IEnumerable<T>: copied for ScoreTally.
    private List<ScoreLine> CopyLines()
    {
        List<ScoreLine> lines = new(ScoreLines.Count);
        foreach (ScoreLine line in ScoreLines)
            lines.Add(line);
        return lines;
    }

    #endregion
}