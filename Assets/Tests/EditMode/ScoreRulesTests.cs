using System.Collections.Generic;
using NUnit.Framework;

// Who leads, how much gold a loss takes, when the game ends.
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
    public void Withdraw_EnoughGold_TakesTheWholeAmount()
    {
        Assert.AreEqual(25, ScoreRules.Withdraw(100, 25));
    }

    [Test]
    public void Withdraw_NotEnoughGold_TakesWhatIsLeft()
    {
        Assert.AreEqual(10, ScoreRules.Withdraw(10, 25));
    }

    [Test]
    public void Withdraw_NoGold_TakesNothing()
    {
        Assert.AreEqual(0, ScoreRules.Withdraw(0, 25));
    }

    [Test]
    public void Withdraw_NegativeAmount_TakesNothing()
    {
        Assert.AreEqual(0, ScoreRules.Withdraw(100, -5));
    }

    [Test]
    public void IsSomeoneBroke_EveryoneHasGold_IsFalse()
    {
        Assert.IsFalse(ScoreRules.IsSomeoneBroke(new[] { 100, 15, 40 }));
    }

    [Test]
    public void IsSomeoneBroke_OnePlayerAtZero_IsTrue()
    {
        Assert.IsTrue(ScoreRules.IsSomeoneBroke(new[] { 100, 0, 40 }));
    }

    [Test]
    public void IsSomeoneBroke_NoPlayers_IsFalse()
    {
        Assert.IsFalse(ScoreRules.IsSomeoneBroke(new int[0]));
    }
}
