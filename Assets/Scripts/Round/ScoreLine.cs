using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;

// One line of a player's round on the scoreboard ("Survived an explosion 9 Points !").
// Not named RoundScore: that is the MonoBehaviour on the scoreboard's text lines (UI/Scoreboard).
// GameRoundManager keeps them in a NetworkList: NGO serializes and replicates the changes itself.
public struct ScoreLine : INetworkSerializable, IEquatable<ScoreLine>
{
    public ulong PlayerId;
    public int Points;
    public FixedString64Bytes Reason;

    public ScoreLine(ulong playerId, int points, string reason)
    {
        PlayerId = playerId;
        Points = points;
        Reason = default;
        // Cut rather than throw if a reason ever gets longer than the 61 bytes the string can hold.
        Reason.CopyFromTruncated(reason ?? string.Empty);
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerId);
        serializer.SerializeValue(ref Points);
        serializer.SerializeValue(ref Reason);
    }

    public bool Equals(ScoreLine other)
    {
        return PlayerId == other.PlayerId && Points == other.Points && Reason.Equals(other.Reason);
    }

    public override bool Equals(object obj) => obj is ScoreLine other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(PlayerId, Points, Reason.GetHashCode());
}

// Pure score arithmetic, tested in EditMode.
public static class ScoreTally
{
    // The points of the round, added up per player.
    public static Dictionary<ulong, int> SumByPlayer(IEnumerable<ScoreLine> scores)
    {
        Dictionary<ulong, int> totals = new();

        foreach (ScoreLine score in scores)
        {
            totals.TryGetValue(score.PlayerId, out int total);
            totals[score.PlayerId] = total + score.Points;
        }

        return totals;
    }

    // One player's lines, in the order they were scored (the scoreboard plays them in that order).
    public static List<ScoreLine> ForPlayer(IEnumerable<ScoreLine> scores, ulong playerId)
    {
        List<ScoreLine> lines = new();

        foreach (ScoreLine score in scores)
        {
            if (score.PlayerId == playerId)
                lines.Add(score);
        }

        return lines;
    }
}