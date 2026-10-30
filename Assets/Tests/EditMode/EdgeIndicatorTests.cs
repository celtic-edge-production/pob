using NUnit.Framework;

public class EdgeIndicatorTests
{
    private const float Margin = 0.05f;
    private const float Tolerance = 1e-4f;

    [Test]
    public void IsOnScreen_Centre_IsTrue()
    {
        Assert.IsTrue(EdgeIndicator.IsOnScreen(0.5f, 0.5f, Margin));
    }

    [Test]
    public void IsOnScreen_OutsideOrInTheMargin_IsFalse()
    {
        Assert.IsFalse(EdgeIndicator.IsOnScreen(1.2f, 0.5f, Margin));
        Assert.IsFalse(EdgeIndicator.IsOnScreen(0.5f, -0.1f, Margin));
        Assert.IsFalse(EdgeIndicator.IsOnScreen(0.98f, 0.5f, Margin));
    }

    [Test]
    public void PlaceOnEdge_TargetToTheRight_SitsOnTheRightEdgePointingRight()
    {
        EdgeIndicator.PlaceOnEdge(2f, 0.5f, false, Margin, out float x, out float y, out float angle);

        Assert.AreEqual(1f - Margin, x, Tolerance);
        Assert.AreEqual(0.5f, y, Tolerance);
        Assert.AreEqual(0f, angle, Tolerance);
    }

    [Test]
    public void PlaceOnEdge_TargetAbove_SitsOnTheTopEdgePointingUp()
    {
        EdgeIndicator.PlaceOnEdge(0.5f, 3f, false, Margin, out float x, out float y, out float angle);

        Assert.AreEqual(0.5f, x, Tolerance);
        Assert.AreEqual(1f - Margin, y, Tolerance);
        Assert.AreEqual(90f, angle, Tolerance);
    }

    [Test]
    public void PlaceOnEdge_TargetBottomLeft_SitsOnTheNearestEdgeAlongTheLine()
    {
        // Twice as far down as left: the line crosses the bottom edge first.
        EdgeIndicator.PlaceOnEdge(0f, -0.5f, false, Margin, out float x, out float y, out float angle);

        Assert.AreEqual(Margin, y, Tolerance);
        Assert.AreEqual(0.5f - 0.225f, x, Tolerance);
        Assert.IsTrue(angle < -90f && angle > -180f);
    }

    [Test]
    public void PlaceOnEdge_TargetBehindTheCamera_IsFlipped()
    {
        EdgeIndicator.PlaceOnEdge(0.7f, 0.5f, true, Margin, out float x, out _, out float angle);

        Assert.AreEqual(Margin, x, Tolerance);
        Assert.AreEqual(180f, System.Math.Abs(angle), Tolerance);
    }
}
