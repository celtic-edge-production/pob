using System.Collections.Generic;

// The players of the session, by client id, plus the local one. Plain C# (no Unity, no NGO) so it
// can be tested in EditMode; Players is the game-facing access point.
public class PlayerRegistry<TPlayer> where TPlayer : class
{
    private readonly Dictionary<ulong, TPlayer> _byClientId = new();
    private readonly List<TPlayer> _all = new();

    public IReadOnlyList<TPlayer> All => _all;
    public int Count => _all.Count;
    public TPlayer Local { get; private set; }

    // Registering a client id twice replaces the previous player (a stale object from an old
    // session must never shadow the live one).
    public void Register(ulong clientId, TPlayer player, bool isLocal)
    {
        if (player == null)
            return;

        if (_byClientId.TryGetValue(clientId, out TPlayer previous))
            Remove(clientId, previous);

        _byClientId[clientId] = player;
        _all.Add(player);

        if (isLocal)
            Local = player;
    }

    // Only removes this exact player: a late despawn of a replaced object leaves its successor alone.
    public void Unregister(ulong clientId, TPlayer player)
    {
        if (player == null)
            return;

        if (_byClientId.TryGetValue(clientId, out TPlayer registered) && ReferenceEquals(registered, player))
            Remove(clientId, player);
    }

    public bool TryGet(ulong clientId, out TPlayer player)
    {
        return _byClientId.TryGetValue(clientId, out player);
    }

    public bool Contains(ulong clientId) => _byClientId.ContainsKey(clientId);

    public void Clear()
    {
        _byClientId.Clear();
        _all.Clear();
        Local = null;
    }

    private void Remove(ulong clientId, TPlayer player)
    {
        _byClientId.Remove(clientId);
        _all.Remove(player);

        if (ReferenceEquals(Local, player))
            Local = null;
    }
}