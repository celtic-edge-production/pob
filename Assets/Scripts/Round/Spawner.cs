using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class Spawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    public Vector3[] spawnPoints; // Tableau des points de spawn pr�d�finis

    // L'attribut [Rpc(SendTo.Server)] etait pose ici, mais Spawner herite de MonoBehaviour et
    // non de NetworkBehaviour : NGO ignore silencieusement cet attribut dans ce cas,
    // ce n'a donc jamais ete un vrai RPC reseau, juste une methode C# normale executee
    // localement sur qui l'appelle. Sans impact actuel car tous les appelants actuels
    // (PhaseManager) sont deja server-only par ailleurs, mais trompeur. Retire pour
    // eviter de laisser croire a une autorite serveur qui n'existe pas.
    public void ResetCharactersPosition()
    {
        PlayerCharacter[] players = FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);

        if (spawnPoints.Length < players.Length)
        {
            Debug.LogError("Pas assez de points de spawn pour le nombre de joueurs !");
            return;
        }

        // Liste temporaire pour suivre les points d�j� utilis�s
        List<Vector3> availableSpawns = new List<Vector3>(spawnPoints);

        foreach (PlayerCharacter player in players)
        {
            // S�lectionne un point al�atoire parmi les spawns disponibles
            int randomIndex = Random.Range(0, availableSpawns.Count);
            Vector3 chosenSpawn = availableSpawns[randomIndex];

            chosenSpawn.z = player.transform.position.z;

            // Supprime le point de la liste pour �viter qu'il soit r�utilis�
            availableSpawns.RemoveAt(randomIndex);

            // D�place le joueur au point choisi
            player.ResetPositionClientRpc(chosenSpawn);
        }
    }
}
