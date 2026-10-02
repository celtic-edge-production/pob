using Unity.Netcode;
using UnityEngine;

public class APhase : NetworkBehaviour
{
    [SerializeField] protected bool isCurrentPhase;
    [SerializeField] protected GameObject _SpawnTileMap;

    virtual protected void Awake()
    {   
        
    }

    virtual protected void Update()
    {
        if (!isCurrentPhase) return;
    }

    virtual protected void FixedUpdate()
    {
        if (!isCurrentPhase) return;
    }

    [Rpc(SendTo.Server)]
    virtual public void StartPhaseServerRpc()
    {

    }

    /*
     * @brief Switch the state of the gameobject with the given tag
     *
     * @param tag The tag of the gameobject to switch state
     * "Cursor" or "Player"
     */
    protected void SwitchStateOfTagGameObject(string tag)
    {
        // Find All gameobject with Cursor Tag
        GameObject[] cursor = GameObject.FindGameObjectsWithTag(tag);

        foreach (GameObject obj in cursor)
        {
            bool isActive = obj.activeSelf;
            obj.SetActive(!isActive);
        }

        return;
    }

    // Note : SwitchStateOfTagGameObject ci-dessus n'est appelee nulle part non plus,
    // mais son corps fonctionne correctement s'il l'etait un jour - laissee en place.
    // FindInActiveObjectByTag (code mort ET casse - SetActive(activeSelf) ne faisait
    // rien) a ete supprimee ici.

    [Rpc(SendTo.Server)]
    protected void SwitchStatePlayersServerRpc()
    {
        if (!IsHost) return;

        // Find all game objects with names AU_PLAYER
        GameObject[] players = GameObject.FindGameObjectsWithTag("AuPlayer");

        foreach (GameObject player in players)
        {
            // Get Au_Player Controller
            PlayerIdentity playerController = player.GetComponent<PlayerIdentity>();

            playerController.SwitchStatePlayerClientRpc();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    protected void SwitchTileMapStateClientRpc(bool value)
    {
        _SpawnTileMap.SetActive(value);
    }
}
