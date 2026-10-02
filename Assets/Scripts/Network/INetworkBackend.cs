// Common contract between the Steam and EOS network backends. LobbyMatchmaking talks
// only to this interface, never directly to Steamworks.NET or the EOS SDK - this is
// what lets the itch.io build swap to EOS without touching any gameplay/UI code.
public interface INetworkBackend
{
    bool IsReady { get; }

    // Creates a lobby/session and starts NGO as host once ready.
    // onCodeReady is called with the short join code to display to the player.
    void Host(System.Action<string> onCodeReady, System.Action<string> onError);

    // Looks up a lobby/session by its short code and starts NGO as client once found.
    void Join(string code, System.Action<string> onError);

    void Disconnect();
}