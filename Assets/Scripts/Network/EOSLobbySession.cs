// Mirrors SteamLobbySession's role for EOS. Fields left generic (string-based) since
// the real EOS SDK's session/lobby handle types depend on which plugin gets installed.
public static class EOSLobbySession
{
    public static string CurrentLobbyId;
    public static string RoomCode;

    public static void Clear()
    {
        CurrentLobbyId = null;
        RoomCode = null;
    }
}