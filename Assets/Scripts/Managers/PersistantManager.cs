using UnityEngine;

public class PersistantManager : MonoBehaviour {

    public void Start()
    {
        PlayerIdentity[] players = FindObjectsByType<PlayerIdentity>(FindObjectsSortMode.None);

        foreach (PlayerIdentity player in players)
            DontDestroyOnLoad(player.gameObject);
    }
}
