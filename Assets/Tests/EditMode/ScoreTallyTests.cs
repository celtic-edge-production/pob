using System.Collections.Generic;
using NUnit.Framework;

public class ScoreTallyTests
{
    [Test]
    public void SumByPlayer_AddsEachPlayersLines()
    {
        ScoreLine[] scores =
        {
            new ScoreLine(1, 9, "Survived an explosion"),
            new ScoreLine(2, 9, "Survived an explosion"),
            new ScoreLine(1, 2, "Assist"),
            new ScoreLine(1, 9, "Survived an explosion"),
        };

        Dictionary<ulong, int> totals = ScoreTally.SumByPlayer(scores);

        Assert.AreEqual(2, totals.Count);
        Assert.AreEqual(20, totals[1]);
        Assert.AreEqual(9, totals[2]);
    }

    [Test]
    public void SumByPlayer_PlayerWithOnlyAnAssist_StillGetsIt()
    {
        ScoreLine[] scores = { new ScoreLine(1, 2, "Assist") };

        Assert.AreEqual(2, ScoreTally.SumByPlayer(scores)[1]);
    }

    [Test]
    public void SumByPlayer_NoScores_IsEmpty()
    {
        Assert.AreEqual(0, ScoreTally.SumByPlayer(new ScoreLine[0]).Count);
    }

    [Test]
    public void ForPlayer_KeepsOnlyThatPlayerInScoringOrder()
    {
        ScoreLine first = new ScoreLine(3, 6, "Survived an explosion");
        ScoreLine second = new ScoreLine(3, 2, "Assist");
        ScoreLine[] scores = { first, new ScoreLine(4, 6, "Survived an explosion"), second };

        List<ScoreLine> lines = ScoreTally.ForPlayer(scores, 3);

        Assert.AreEqual(2, lines.Count);
        Assert.AreEqual(first, lines[0]);
        Assert.AreEqual(second, lines[1]);
    }

    [Test]
    public void ForPlayer_UnknownPlayer_IsEmpty()
    {
        ScoreLine[] scores = { new ScoreLine(1, 9, "Survived an explosion") };

        Assert.AreEqual(0, ScoreTally.ForPlayer(scores, 8).Count);
    }

    [Test]
    public void RoundScore_KeepsReasonAndPoints()
    {
        ScoreLine score = new ScoreLine(5, 2, "Assist");

        Assert.AreEqual("Assist", score.Reason.ToString());
        Assert.AreEqual(2, score.Points);
        Assert.AreEqual(5UL, score.PlayerId);
    }

    [Test]
    public void RoundScore_TooLongReason_IsCutNotThrown()
    {
        ScoreLine score = new ScoreLine(1, 1, new string('x', 200));

        Assert.IsTrue(score.Reason.Length > 0);
        Assert.IsTrue(score.Reason.Length <= 61);
    }
}