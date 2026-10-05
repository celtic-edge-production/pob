using System.Collections.Generic;
using NUnit.Framework;

public class GameRoundManagerTests
{
    [Test]
    public void ComputeTotalScoreDeltas_SumsAllRoundScoresPerPlayer()
    {
        var roundScores = new GameRoundManager.CustomDictionary();
        roundScores.Add(1UL, new List<GameRoundManager.IntStringPair>
        {
            new GameRoundManager.IntStringPair(18, "Survived the round"),
            new GameRoundManager.IntStringPair(2, "Assist"),
        });
        roundScores.Add(2UL, new List<GameRoundManager.IntStringPair>
        {
            new GameRoundManager.IntStringPair(5, "Something else"),
        });

        var result = GameRoundManager.ComputeTotalScoreDeltas(roundScores);

        Assert.AreEqual(20, result[1UL]);
        Assert.AreEqual(5, result[2UL]);
    }

    [Test]
    public void ComputeTotalScoreDeltas_WithNoWinner_StillReturnsAllPlayerScores()
    {
        var roundScores = new GameRoundManager.CustomDictionary();
        roundScores.Add(1UL, new List<GameRoundManager.IntStringPair>
        {
            new GameRoundManager.IntStringPair(2, "Assist"),
        });

        var result = GameRoundManager.ComputeTotalScoreDeltas(roundScores);

        Assert.AreEqual(2, result[1UL]);
    }

    [Test]
    public void ComputeTotalScoreDeltas_WithEmptyScores_ReturnsEmptyResult()
    {
        var roundScores = new GameRoundManager.CustomDictionary();

        var result = GameRoundManager.ComputeTotalScoreDeltas(roundScores);

        Assert.AreEqual(0, result.Count);
    }

    [Test]
    public void ComputeWinnerIds_SingleLeader_ReturnsOnlyThatPlayer()
    {
        var totals = new Dictionary<ulong, int> { { 0, 10 }, { 1, 25 }, { 2, 5 } };
        CollectionAssert.AreEqual(new ulong[] { 1 }, GameRoundManager.ComputeWinnerIds(totals));
    }

    [Test]
    public void ComputeWinnerIds_Tie_ReturnsAllTiedPlayersSorted()
    {
        var totals = new Dictionary<ulong, int> { { 2, 30 }, { 1, 10 }, { 0, 30 } };
        CollectionAssert.AreEqual(new ulong[] { 0, 2 }, GameRoundManager.ComputeWinnerIds(totals));
    }

    [Test]
    public void ComputeWinnerIds_NoScores_ReturnsEmpty()
    {
        Assert.IsEmpty(GameRoundManager.ComputeWinnerIds(new Dictionary<ulong, int>()));
    }
}