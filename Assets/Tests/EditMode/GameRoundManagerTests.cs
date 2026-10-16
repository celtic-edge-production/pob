using System.Collections.Generic;
using NUnit.Framework;

// The round score sums moved to ScoreTally (ScoreTallyTests).
public class GameRoundManagerTests
{
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