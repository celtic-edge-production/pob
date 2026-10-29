using System.Collections.Generic;
using UnityEngine;

// Every peer: the players of the session, filled by PlayerIdentity when it spawns and despawns.
// Replaces the FindObjectsByType<PlayerIdentity> scans and the character lookups through the
// hierarchy (the character of a player is replicated: PlayerIdentity.Character).
public static class Players
{
    private static readonly PlayerRegistry<PlayerIdentity> s_registry = new();

    // Every spawned player, in spawn order.
    public static IReadOnlyList<PlayerIdentity> All => s_registry.All;

    public static int Count => s_registry.Count;

    // This machine's player, null until it has spawned.
    public static PlayerIdentity Local => s_registry.Local;

    public static bool TryGet(ulong clientId, out PlayerIdentity player)
    {
        return s_registry.TryGet(clientId, out player) && player != null;
    }

    // The player controlling this character, or null (free lobby character).
    public static PlayerIdentity FindByCharacter(PlayerCharacter character)
    {
        if (character == null)
            return null;

        foreach (PlayerIdentity player in s_registry.All)
        {
            if (player != null && player.Character == character)
                return player;
        }
        return null;
    }

    // The name shown for a player: his character's (the same on every peer), or "Player N".
    public static string NameOf(ulong clientId)
    {
        PlayerCharacter character = TryGet(clientId, out PlayerIdentity player) ? player.Character : null;
        return character != null ? character.name : $"Player {clientId}";
    }

    // The characters of the players who have one.
    public static IEnumerable<PlayerCharacter> Characters
    {
        get
        {
            foreach (PlayerIdentity player in s_registry.All)
            {
                PlayerCharacter character = player != null ? player.Character : null;
                if (character != null)
                    yield return character;
            }
        }
    }

    internal static void Register(PlayerIdentity player) =>
        s_registry.Register(player.OwnerClientId, player, player.IsOwner);

    internal static void Unregister(PlayerIdentity player) =>
        s_registry.Unregister(player.OwnerClientId, player);

    // Static state survives "Enter Play Mode" without a domain reload: start every play session empty.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => s_registry.Clear();
}