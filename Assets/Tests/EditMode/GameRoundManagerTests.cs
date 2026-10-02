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
}