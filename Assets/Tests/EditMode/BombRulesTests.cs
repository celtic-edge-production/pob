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
}