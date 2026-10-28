using System.Collections.Generic;
using NUnit.Framework;

// Who wins and when the game ends (formerly ScoreKeeperTests).
public class ScoreRulesTests
{
    [Test]
    public void ComputeWinnerIds_SingleLeader_ReturnsOnlyThatPlayer()
    {
        var totals = new Dictionary<ulong, int> { { 0, 10 }, { 1, 25 }, { 2, 5 } };
        CollectionAssert.AreEqual(new ulong[] { 1 }, ScoreRules.ComputeWinnerIds(totals));
    }

    [Test]
    public void ComputeWinnerIds_Tie_ReturnsAllTiedPlayersSorted()
    {
        var totals = new Dictionary<ulong, int> { { 2, 30 }, { 1, 10 }, { 0, 30 } };
        CollectionAssert.AreEqual(new ulong[] { 0, 2 }, ScoreRules.ComputeWinnerIds(totals));
    }

    [Test]
    public void ComputeWinnerIds_NoScores_ReturnsEmpty()
    {
        Assert.IsEmpty(ScoreRules.ComputeWinnerIds(new Dictionary<ulong, int>()));
    }

    [Test]
    public void IsGameWon_BeforeLastRoundAndBelowTarget_IsFalse()
    {
        Assert.IsFalse(ScoreRules.IsGameWon(3, 25, new[] { 10, 40 }, 90));
    }

    [Test]
    public void IsGameWon_LastRoundPlayed_IsTrue()
    {
        Assert.IsTrue(ScoreRules.IsGameWon(25, 25, new[] { 10, 40 }, 90));
    }

    [Test]
    public void IsGameWon_TargetScoreReached_IsTrue()
    {
        Assert.IsTrue(ScoreRules.IsGameWon(3, 25, new[] { 10, 90 }, 90));
    }

    [Test]
    public void IsGameWon_NoScoresYet_IsFalse()
    {
        Assert.IsFalse(ScoreRules.IsGameWon(0, 25, new int[0], 90));
    }
}
