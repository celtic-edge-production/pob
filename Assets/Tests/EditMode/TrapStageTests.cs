using NUnit.Framework;

public class TrapStageTests
{
    [TestCase(TrapStage.InChest, TrapStage.Chosen)]
    [TestCase(TrapStage.Chosen, TrapStage.Placing)]
    [TestCase(TrapStage.Placing, TrapStage.Placed)]
    [TestCase(TrapStage.Placed, TrapStage.Active)]
    [TestCase(TrapStage.Active, TrapStage.Placed)]
    public void CanMove_AlongTheLifecycle(TrapStage from, TrapStage to)
    {
        Assert.IsTrue(TrapStageRules.CanMove(from, to));
    }

    [TestCase(TrapStage.InChest, TrapStage.Placing)]   // must be chosen first
    [TestCase(TrapStage.InChest, TrapStage.Placed)]
    [TestCase(TrapStage.Chosen, TrapStage.Placed)]     // must wait for the chest to close
    [TestCase(TrapStage.Placing, TrapStage.Active)]
    [TestCase(TrapStage.Placed, TrapStage.Placing)]    // a placed trap never goes back to a player
    [TestCase(TrapStage.Active, TrapStage.InChest)]
    [TestCase(TrapStage.Chosen, TrapStage.Chosen)]     // choosing twice
    public void CanMove_RefusesSkipsAndGoingBack(TrapStage from, TrapStage to)
    {
        Assert.IsFalse(TrapStageRules.CanMove(from, to));
    }

    [TestCase(TrapStage.InChest, true)]
    [TestCase(TrapStage.Chosen, true)]
    [TestCase(TrapStage.Placing, true)]
    [TestCase(TrapStage.Placed, false)]
    [TestCase(TrapStage.Active, false)]
    public void IsPending_UntilPlaced(TrapStage stage, bool expected)
    {
        Assert.AreEqual(expected, TrapStageRules.IsPending(stage));
    }

    [TestCase(TrapStage.InChest, false)]
    [TestCase(TrapStage.Chosen, true)]
    [TestCase(TrapStage.Placing, true)]
    [TestCase(TrapStage.Placed, false)]
    public void IsHeld_BetweenChoiceAndPlacement(TrapStage stage, bool expected)
    {
        Assert.AreEqual(expected, TrapStageRules.IsHeld(stage));
    }

    [TestCase(TrapStage.Chosen, false)]
    [TestCase(TrapStage.Placed, true)]
    [TestCase(TrapStage.Active, true)]
    public void IsOnMap_OncePlaced(TrapStage stage, bool expected)
    {
        Assert.AreEqual(expected, TrapStageRules.IsOnMap(stage));
    }
}