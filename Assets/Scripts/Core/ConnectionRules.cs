// Who may join a session. Plain C#, tested in EditMode; ConnectionGate applies it in NGO.
public static class ConnectionRules
{
    // One player per character in the lobby.
    public const int DefaultMaxPlayers = 4;

    public const string GameInProgressReason = "Une partie est en cours : réessaie quand elle sera terminée.";

    public static string LobbyFullReason(int maxPlayers) => $"Le lobby est complet ({maxPlayers} joueurs).";

    // Null when the player may join, otherwise the reason shown to him.
    // connectedPlayers counts everyone already in, the host included.
    public static string RefusalReason(int connectedPlayers, int maxPlayers, bool gameInProgress)
    {
        if (gameInProgress)
            return GameInProgressReason;

        if (connectedPlayers >= maxPlayers)
            return LobbyFullReason(maxPlayers);

        return null;
    }
}
