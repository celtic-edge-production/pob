using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneNames
{
    public const string MainMenu = "Main Menu";
    public const string Lobby = "Lobby";
}

// The one way to change scene during a session: the server loads it and NGO's scene management
// takes every peer along. Replaces SceneManagerNetwork, LobbyManager.ChangeSceneServerRpc and
// GameRoundManager.LoadLobby, each of which had its own checks (or none).
public static class SceneLoader
{
    // Server only. False when the session cannot load a scene (not the server, stopping, already loading).
    public static bool LoadForEveryone(string sceneName)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer || !networkManager.IsListening
            || networkManager.ShutdownInProgress || networkManager.SceneManager == null)
        {
            Debug.LogWarning($"[SceneLoader] Cannot load {sceneName}: no running server session.");
            return false;
        }

        SceneEventProgressStatus status = networkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogWarning($"[SceneLoader] Loading {sceneName} failed: {status}.");
            return false;
        }

        return true;
    }
}
