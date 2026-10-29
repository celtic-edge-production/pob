using System;
using System.Collections.Generic;
using System.Linq;

// The gold of a game: who leads, how much can be taken, when the game ends. Pure, tested in
// EditMode; ScoreKeeper and GameRoundManager apply it on the server.
public static class ScoreRules
{
    // Every player sharing the most gold leads (ties are possible: sudden death).
    public static ulong[] ComputeWinnerIds(IReadOnlyDictionary<ulong, int> totals)
    {
        if (totals.Count == 0)
            return Array.Empty<ulong>();

        int best = totals.Values.Max();
        return totals.Where(kv => kv.Value == best).Select(kv => kv.Key).OrderBy(id => id).ToArray();
    }

    // What a loss really takes: never more than the player has, never negative.
    public static int Withdraw(int gold, int amount)
    {
        return Math.Max(0, Math.Min(gold, amount));
    }

    // The game ends as soon as one player has no gold left.
    public static bool IsSomeoneBroke(IEnumerable<int> golds)
    {
        return golds.Any(gold => gold <= 0);
    }
}
