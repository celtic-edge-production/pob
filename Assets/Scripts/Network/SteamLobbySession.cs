using Steamworks;

// Petit conteneur statique, rempli par CreateJoinRoomManager au moment de la création
// ou de la connexion au lobby Steam, et lu par LobbyManager (scène "Lobby") pour
// réafficher le code aux joueurs sans avoir à refaire un appel Steam.
public static class SteamLobbySession
{
    public static CSteamID CurrentLobbyId;
    public static string RoomCode;

    public static void Clear()
    {
        CurrentLobbyId = CSteamID.Nil;
        RoomCode = null;
    }
}
