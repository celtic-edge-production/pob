using NUnit.Framework;

public class BombRulesTests
{
    [Test]
    public void CanTransfer_ToEligiblePlayer_ReturnsTrue()
    {
        Assert.IsTrue(BombRules.CanTransfer(currentHolderId: 1, newHolderId: 2, newHolderIsAlive: true, newHolderIsInvincible: false));
    }

    [Test]
    public void CanTransfer_ToInvinciblePlayer_ReturnsFalse()
    {
        Assert.IsFalse(BombRules.CanTransfer(currentHolderId: 1, newHolderId: 2, newHolderIsAlive: true, newHolderIsInvincible: true));
    }

    [Test]
    public void CanTransfer_ToSameHolder_ReturnsFalse()
    {
        Assert.IsFalse(BombRules.CanTransfer(currentHolderId: 1, newHolderId: 1, newHolderIsAlive: true, newHolderIsInvincible: false));
    }

    [Test]
    public void CanTransfer_ToDeadPlayer_ReturnsFalse()
    {
        Assert.IsFalse(BombRules.CanTransfer(currentHolderId: 1, newHolderId: 2, newHolderIsAlive: false, newHolderIsInvincible: false));
    }

    [Test]
    public void IsWithinPassRange_Touching_ReturnsTrue()
    {
        Assert.IsTrue(BombRules.IsWithinPassRange(0f, 0f, 1f, 0.5f, 2f));
    }

    [Test]
    public void IsWithinPassRange_ExactlyAtTheLimit_ReturnsTrue()
    {
        Assert.IsTrue(BombRules.IsWithinPassRange(1f, 1f, 1f, 3f, 2f));
    }

    [Test]
    public void IsWithinPassRange_FarAway_ReturnsFalse()
    {
        Assert.IsFalse(BombRules.IsWithinPassRange(0f, 0f, 3f, 0f, 2f));
    }

    [Test]
    public void FuseAfterPass_PlentyOfTimeLeft_IsUnchanged()
    {
        Assert.AreEqual(30.0, BombRules.FuseAfterPass(30.0, 8.0));
    }

    [Test]
    public void FuseAfterPass_AlmostOut_GivesTheMinimum()
    {
        Assert.AreEqual(8.0, BombRules.FuseAfterPass(2.0, 8.0));
    }

    [Test]
    public void UsesMinimum_OnlyBelowIt()
    {
        Assert.IsTrue(BombRules.UsesMinimum(2.0, 8.0));
        Assert.IsFalse(BombRules.UsesMinimum(8.0, 8.0));
        Assert.IsFalse(BombRules.UsesMinimum(30.0, 8.0));
    }

    [Test]
    public void NextMinimum_ShrinksByTheStep()
    {
        Assert.AreEqual(6f, BombRules.NextMinimum(8f, 2f, 3f));
    }

    [Test]
    public void NextMinimum_NeverBelowTheLowest()
    {
        Assert.AreEqual(3f, BombRules.NextMinimum(4f, 2f, 3f));
        Assert.AreEqual(3f, BombRules.NextMinimum(3f, 2f, 3f));
    }
}
