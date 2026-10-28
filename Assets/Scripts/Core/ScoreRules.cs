using System;
using System.Collections.Generic;
using System.Linq;

// Who wins and when the game ends. Pure, tested in EditMode; ScoreKeeper applies it on the server.
public static class ScoreRules
{
    // Every player sharing the best total wins (ties are possible).
    public static ulong[] ComputeWinnerIds(IReadOnlyDictionary<ulong, int> totals)
    {
        if (totals.Count == 0)
            return Array.Empty<ulong>();

        int best = totals.Values.Max();
        return totals.Where(kv => kv.Value == best).Select(kv => kv.Key).OrderBy(id => id).ToArray();
    }

    // Enough rounds played, or someone reached the target score.
    public static bool IsGameWon(int roundsPlayed, int maxRounds, IEnumerable<int> totalScores, int maxScore)
    {
        return roundsPlayed >= maxRounds || totalScores.Any(score => score >= maxScore);
    }
}
