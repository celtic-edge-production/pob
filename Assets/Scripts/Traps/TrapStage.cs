// Where a trap is in its life. Replaces the former needToBeSelected / isSetable / isPlaced flags
// and the three server lists that had to be kept consistent by hand.
public enum TrapStage : byte
{
    // Offered in the chest: any player without a trap may pick it.
    InChest,
    // Picked by a player; the chest is still open, the preview can already follow the cursor.
    Chosen,
    // The chest has closed: the player may place it.
    Placing,
    // On the grid, harmless (phase 1).
    Placed,
    // On the grid and live (phase 2): collisions and animations on.
    Active,
}

// The allowed moves between stages. Plain C#, tested in EditMode (TrapStageTests).
public static class TrapStageRules
{
    public static bool CanMove(TrapStage from, TrapStage to)
    {
        switch (from)
        {
            case TrapStage.InChest: return to == TrapStage.Chosen;
            case TrapStage.Chosen: return to == TrapStage.Placing;
            case TrapStage.Placing: return to == TrapStage.Placed;
            case TrapStage.Placed: return to == TrapStage.Active;
            case TrapStage.Active: return to == TrapStage.Placed;
            default: return false;
        }
    }

    // Still waiting for a player: phase 1 cannot end while such a trap exists.
    public static bool IsPending(TrapStage stage)
    {
        return stage == TrapStage.InChest || stage == TrapStage.Chosen || stage == TrapStage.Placing;
    }

    // Held by a player who has not placed it yet.
    public static bool IsHeld(TrapStage stage)
    {
        return stage == TrapStage.Chosen || stage == TrapStage.Placing;
    }

    // On the map (placed traps are hidden while the chest is open, shown again when it closes).
    public static bool IsOnMap(TrapStage stage)
    {
        return stage == TrapStage.Placed || stage == TrapStage.Active;
    }
}