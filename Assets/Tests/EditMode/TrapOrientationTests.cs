using NUnit.Framework;

public class TrapOrientationTests
{
    // An L: its mirror and its rotations are all different, so every mistake shows.
    private static readonly TrapShape LShape = new TrapShape(new int[,] { { 1, 0 }, { 1, 0 }, { 1, 1 } });

    [TestCase(4, 0)]
    [TestCase(-1, 3)]
    [TestCase(5, 1)]
    public void Constructor_KeepsTurnsBetweenZeroAndThree(int turns, int expected)
    {
        Assert.AreEqual(expected, new TrapOrientation(turns, false).QuarterTurns);
    }

    [Test]
    public void Rotated_AddsTurns()
    {
        TrapOrientation orientation = TrapOrientation.Default.Rotated(1).Rotated(1);

        Assert.AreEqual(2, orientation.QuarterTurns);
        Assert.IsFalse(orientation.Mirrored);
    }

    [Test]
    public void Rotated_LeftThenRight_GivesTheSameOrientation()
    {
        Assert.AreEqual(TrapOrientation.Default, TrapOrientation.Default.Rotated(1).Rotated(-1));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void FlippedHorizontally_Twice_GivesTheSameOrientation(int turns)
    {
        TrapOrientation orientation = new TrapOrientation(turns, false);

        Assert.AreEqual(orientation, orientation.FlippedHorizontally().FlippedHorizontally());
    }

    // The flip is a left-right mirror of what is on screen, whatever the rotation.
    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(3, false)]
    [TestCase(1, true)]
    [TestCase(3, true)]
    public void FlippedHorizontally_MirrorsTheCellsOnScreen(int turns, bool mirrored)
    {
        TrapOrientation orientation = new TrapOrientation(turns, mirrored);

        int[,] expected = orientation.Apply(LShape).MirroredHorizontally().ToMatrix();
        int[,] actual = orientation.FlippedHorizontally().Apply(LShape).ToMatrix();

        Assert.AreEqual(expected, actual);
    }

    // A trap aiming left (its art aims down, turned three quarters) aims right once flipped.
    [Test]
    public void FlippedHorizontally_ShooterAimingLeft_AimsRight()
    {
        TrapOrientation aimingLeft = new TrapOrientation(3, false);

        TrapOrientation flipped = aimingLeft.FlippedHorizontally();

        Assert.AreEqual(1, flipped.QuarterTurns);   // down turned a quarter counterclockwise: right
    }

    [Test]
    public void Apply_Default_IsTheBaseShape()
    {
        Assert.AreEqual(LShape.ToMatrix(), TrapOrientation.Default.Apply(LShape).ToMatrix());
    }

    [Test]
    public void Apply_MirrorsBeforeRotating()
    {
        TrapOrientation orientation = new TrapOrientation(1, true);

        Assert.AreEqual(LShape.MirroredHorizontally().Rotated(1).ToMatrix(), orientation.Apply(LShape).ToMatrix());
    }

    [TestCase(0, false, 0f, 1f)]
    [TestCase(1, false, 90f, 1f)]
    [TestCase(3, true, 270f, -1f)]
    public void Transform_MatchesTheOrientation(int turns, bool mirrored, float eulerZ, float scaleXSign)
    {
        TrapOrientation orientation = new TrapOrientation(turns, mirrored);

        Assert.AreEqual(eulerZ, orientation.EulerZ, 1e-5f);
        Assert.AreEqual(scaleXSign, orientation.ScaleXSign, 1e-5f);
    }
}