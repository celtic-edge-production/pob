// Who may receive the bomb. Pure, tested in EditMode; BombManager applies it on the server.
public static class BombRules
{
    // Never to a dead or invincible player, never back to the holder himself.
    public static bool CanTransfer(ulong currentHolderId, ulong newHolderId, bool newHolderIsAlive, bool newHolderIsInvincible)
    {
        if (!newHolderIsAlive || newHolderIsInvincible)
            return false;

        return currentHolderId != newHolderId;
    }
}
