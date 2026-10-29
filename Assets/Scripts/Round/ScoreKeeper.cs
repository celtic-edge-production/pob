using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// The gold of a game: every player's gold, this round's gains and losses, and when the game is
// over (someone has no gold left). The server writes, every peer reads (Scoreboard).
// Lives next to GameRoundManager.
public class ScoreKeeper : NetworkBehaviour
{
    [Tooltip("The gold every player starts the game with.")]
    [SerializeField] private int _startingGold = 100;

    // Every player's gold, changed at once by Change(). Kept under its former name: the scoreboard reads it.
    public NetworkVariable<Dictionary<ulong, int>> PlayerTotalScores { get; private set; }

    // This round's gains and losses, every player mixed, in order. NGO replicates the changes.
    public NetworkList<ScoreLine> ScoreLines = new();

    // The players shown on the scoreboard, in column order (the players of the last round).
    public NetworkList<ulong> PlayerIds = new();

    private void Awake()
    {
        PlayerTotalScores = new NetworkVariable<Dictionary<ulong, int>>(new Dictionary<ulong, int>());
    }

    #region Server

    // A new round with these players: no lines yet, the starting gold for a newcomer.
    public void BeginRound(IEnumerable<ulong> playerIds)
    {
        ScoreLines.Clear();

        foreach (ulong playerId in playerIds)
        {
            if (!PlayerTotalScores.Value.ContainsKey(playerId))
                PlayerTotalScores.Value[playerId] = _startingGold;
        }

        MarkTotalsDirty();
    }

    // Takes up to `amount` gold from a player (never below 0). Returns what was really taken.
    public int Take(ulong playerId, int amount, string reason)
    {
        int taken = ScoreRules.Withdraw(GoldOf(playerId), amount);
        Change(playerId, -taken, reason);
        return taken;
    }

    public void Give(ulong playerId, int amount, string reason)
    {
        if (amount > 0)
            Change(playerId, amount, reason);
    }

    private void Change(ulong playerId, int delta, string reason)
    {
        if (!PlayerTotalScores.Value.ContainsKey(playerId))
            return;

        PlayerTotalScores.Value[playerId] += delta;
        ScoreLines.Add(new ScoreLine(playerId, delta, reason));
        MarkTotalsDirty();
    }

    // The round is over: the players still here become the scoreboard's columns.
    public void CloseRound(IReadOnlyCollection<ulong> playersStillHere)
    {
        PlayerIds.Clear();
        foreach (ulong playerId in playersStillHere)
            PlayerIds.Add(playerId);
    }

    public void RemovePlayer(ulong playerId)
    {
        PlayerTotalScores.Value.Remove(playerId);
        MarkTotalsDirty();
    }

    // True as soon as one player has no gold left: the game is over (or goes to a sudden death).
    public bool IsSomeoneBroke() => ScoreRules.IsSomeoneBroke(PlayerTotalScores.Value.Values);

    // The players with the most gold (several on a tie).
    public ulong[] GetWinnerIds() => ScoreRules.ComputeWinnerIds(PlayerTotalScores.Value);

    // NGO already detects in-place changes by comparing the dictionary with a copy on every
    // network tick. Calling it explicitly documents that it is replicated to the clients.
    private void MarkTotalsDirty()
    {
        PlayerTotalScores.CheckDirtyState();
    }

    #endregion

    #region Every peer

    public int GoldOf(ulong playerId)
    {
        return PlayerTotalScores.Value.TryGetValue(playerId, out int gold) ? gold : 0;
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