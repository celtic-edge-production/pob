using UnityEngine;

public class PersistantManager : MonoBehaviour {

    public void Start()
    {
        PlayerIdentity[] players = FindObjectsByType<PlayerIdentity>(FindObjectsSortMode.None);

        foreach (PlayerIdentity player in players)
            DontDestroyOnLoad(player.gameObject);
    }

    public void RemoveOrphanPlayers()
    {
        PlayerCharacter[] players = FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);

        foreach (PlayerCharacter player in players)
        {
            if (player.transform.parent == null)
                Destroy(player.gameObject);
        }
    }
}
