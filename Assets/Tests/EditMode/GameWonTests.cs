using NUnit.Framework;

public class GameWonTests
{
    [Test]
    public void IsGameWon_BeforeLastRoundAndBelowTarget_IsFalse()
    {
        Assert.IsFalse(GameRoundManager.IsGameWon(3, 25, new[] { 10, 40 }, 90));
    }

    [Test]
    public void IsGameWon_LastRoundPlayed_IsTrue()
    {
        Assert.IsTrue(GameRoundManager.IsGameWon(25, 25, new[] { 10, 40 }, 90));
    }

    [Test]
    public void IsGameWon_TargetScoreReached_IsTrue()
    {
        Assert.IsTrue(GameRoundManager.IsGameWon(3, 25, new[] { 10, 90 }, 90));
    }

    [Test]
    public void IsGameWon_NoScoresYet_IsFalse()
    {
        Assert.IsFalse(GameRoundManager.IsGameWon(0, 25, new int[0], 90));
    }
}