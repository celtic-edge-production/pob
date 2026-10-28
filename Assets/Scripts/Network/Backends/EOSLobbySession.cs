using PlayEveryWare.EpicOnlineServices;
using PlayEveryWare.EpicOnlineServices.Samples;

// Current EOS lobby, written by EOSNetworkBackend and read by LobbyManager to show the code.
public static class EOSLobbySession
{
    public static string CurrentLobbyId;
    public static string RoomCode;

    // Leaves the EOS lobby if this session is in one. Safe to call when EOS is not used.
    public static void Leave()
    {
        if (CurrentLobbyId != null && EOSManager.Instance != null)
            EOSManager.Instance.GetOrCreateManager<EOSLobbyManager>().LeaveLobby(null);

        Clear();
    }

    public static void Clear()
    {
        CurrentLobbyId = null;
        RoomCode = null;
    }
}