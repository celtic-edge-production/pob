using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class PlayerCleaner : MonoBehaviour {

    /*
     * @brief This function will remove the duplication of Characters,
     * and every characters that are already selected and have a duplicate version unselected.
     */
    public void RemoveDuplicatedCharacters()
    {
        // Garde ajoutee : ResetLobby() (l'appelant, dans LobbyManager) s'execute sur
        // TOUS les clients, pas seulement l'hote. Or Despawn() n'est autorise que
        // cote serveur en NGO - sans cette garde, l'appel plantait sur tout client
        // non-hote depuis la correction Destroy(player)->Despawn() ci-dessous.
        if (!Unity.Netcode.NetworkManager.Singleton.IsServer)
            return;

        PlayerCharacter[] players = FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);

        foreach (PlayerCharacter player in players) {
            if (player != null) {
                string playerName = player.gameObject.name;

                PlayerCharacter duplicate = players.FirstOrDefault(p => p != player && p.gameObject.name == playerName);

                // Bug corrige : "Destroy(player)" detruisait seulement le COMPOSANT
                // PlayerCharacter (pas le GameObject), laissant un objet fantome avec
                // son NetworkObject/Rigidbody2D/etc. trainer dans la scene - incoherent
                // avec l'autre branche qui fait correctement Destroy(duplicate.gameObject).
                // Utilise Despawn() plutot que Destroy() direct, meme raison que partout
                // ailleurs dans le projet (objet reseau spawn).
                if (duplicate != null && player.transform.parent == null)
                {
                    NetworkObject playerNetworkObject = player.GetComponent<NetworkObject>();
                    if (playerNetworkObject != null)
                        playerNetworkObject.Despawn(true);
                    else
                        Destroy(player.gameObject);
                }
                else if (duplicate != null && player.transform.parent != null)
                {
                    NetworkObject duplicateNetworkObject = duplicate.GetComponent<NetworkObject>();
                    if (duplicateNetworkObject != null)
                        duplicateNetworkObject.Despawn(true);
                    else
                        Destroy(duplicate.gameObject);
                }
            }
        }
    }
}
