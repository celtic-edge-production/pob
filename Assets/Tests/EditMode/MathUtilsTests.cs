using NUnit.Framework;

public class MathUtilsTests
{
    [Test]
    public void SafeDivisor_WithOnePlayer_ReturnsOneInsteadOfZero()
    {
        int result = MathUtils.SafeDivisor(1);

        Assert.AreEqual(1, result);
    }

    [Test]
    public void SafeDivisor_WithZeroPlayers_ReturnsOneInsteadOfNegativeOne()
    {
        int result = MathUtils.SafeDivisor(0);

        Assert.AreEqual(1, result);
    }

    [Test]
    public void SafeDivisor_WithTwoPlayers_ReturnsOne()
    {
        int result = MathUtils.SafeDivisor(2);

        Assert.AreEqual(1, result);
    }

    [Test]
    public void SafeDivisor_WithFourPlayers_ReturnsThree()
    {
        int result = MathUtils.SafeDivisor(4);

        Assert.AreEqual(3, result);
    }
}