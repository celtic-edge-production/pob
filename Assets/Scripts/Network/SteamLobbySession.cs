using Steamworks;

// Current Steam lobby, written by SteamNetworkBackend and read by LobbyManager to show the code.
public static class SteamLobbySession
{
    public static CSteamID CurrentLobbyId;
    public static string RoomCode;

    // Leaves the Steam lobby if this session is in one. Safe to call when Steam is not used:
    // the default CSteamID is invalid, so no Steam API call is made.
    public static void Leave()
    {
        if (CurrentLobbyId.IsValid())
            SteamMatchmaking.LeaveLobby(CurrentLobbyId);

        Clear();
    }

    public static void Clear()
    {
        CurrentLobbyId = CSteamID.Nil;
        RoomCode = null;
    }
}