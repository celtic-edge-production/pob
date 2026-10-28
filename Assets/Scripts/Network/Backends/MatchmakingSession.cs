// The game's only window on the matchmaking lobby (Steam, EOS or Local): the rest of the game
// never names Steam or EOS, so they stay inside this assembly.
public static class MatchmakingSession
{
    // The join code to show in the lobby, or null (Local backend, or no lobby yet).
    public static string RoomCode
    {
        get
        {
            if (!string.IsNullOrEmpty(SteamLobbySession.RoomCode))
                return SteamLobbySession.RoomCode;

            if (!string.IsNullOrEmpty(EOSLobbySession.RoomCode))
                return EOSLobbySession.RoomCode;

            return null;
        }
    }

    public static bool IsLocal => NetworkBackendSelector.Current == BackendType.Local;

    // Leaves whichever lobby was joined. Safe to call when none was.
    public static void LeaveAll()
    {
        SteamLobbySession.Leave();
        EOSLobbySession.Leave();
    }
}
