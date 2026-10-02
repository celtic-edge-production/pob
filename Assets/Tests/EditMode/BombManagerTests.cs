using NUnit.Framework;

public class BombManagerTests
{
    [Test]
    public void CanTransfer_ToInvinciblePlayer_ReturnsFalse()
    {
        bool result = BombManager.CanTransfer(currentHolderId: 1, newHolderId: 2, newHolderIsInvincible: true);

        Assert.IsFalse(result);
    }

    [Test]
    public void CanTransfer_ToSameHolder_ReturnsFalse()
    {
        // Player already has the bomb - shouldn't "transfer" to themselves.
        bool result = BombManager.CanTransfer(currentHolderId: 1, newHolderId: 1, newHolderIsInvincible: false);

        Assert.IsFalse(result);
    }

    [Test]
    public void CanTransfer_ToEligiblePlayer_ReturnsTrue()
    {
        bool result = BombManager.CanTransfer(currentHolderId: 1, newHolderId: 2, newHolderIsInvincible: false);

        Assert.IsTrue(result);
    }
}