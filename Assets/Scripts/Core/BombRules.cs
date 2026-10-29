using System;

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

    // The fuse after a pass: whoever receives the bomb always has at least `minimum` seconds.
    public static double FuseAfterPass(double remaining, double minimum)
    {
        return Math.Max(remaining, minimum);
    }

    // True when the guaranteed minimum was needed (the fuse was below it): it then shrinks.
    public static bool UsesMinimum(double remaining, double minimum)
    {
        return remaining < minimum;
    }

    // The next guaranteed minimum: `step` seconds less each time it is used, never below `lowest`.
    public static float NextMinimum(float minimum, float step, float lowest)
    {
        return Math.Max(lowest, minimum - step);
    }

    // A pass on contact, as the server sees the two players. Each machine sees the others a little
    // in the past, so the holder can touch a player who has already run away on his own screen:
    // beyond this distance, the contact is refused.
    public static bool IsWithinPassRange(float holderX, float holderY, float targetX, float targetY, float maxDistance)
    {
        float dx = targetX - holderX;
        float dy = targetY - holderY;
        return dx * dx + dy * dy <= maxDistance * maxDistance;
    }
}
