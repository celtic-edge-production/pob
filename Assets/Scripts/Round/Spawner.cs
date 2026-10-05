using UnityEngine;
using System.Collections.Generic;

public class Spawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    public Vector3[] spawnPoints;

    // Server only. Only finds active characters: call it while they are visible.
    public void ResetCharactersPosition()
    {
        PlayerCharacter[] players = FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None);

        if (spawnPoints.Length < players.Length)
        {
            Debug.LogError("Not enough spawn points for the number of players!");
            return;
        }

        List<Vector3> availableSpawns = new List<Vector3>(spawnPoints);

        foreach (PlayerCharacter player in players)
        {
            // Random free spawn point, never used twice in the same reset.
            int randomIndex = Random.Range(0, availableSpawns.Count);
            Vector3 chosenSpawn = availableSpawns[randomIndex];
            availableSpawns.RemoveAt(randomIndex);

            chosenSpawn.z = player.transform.position.z;
            player.ResetPositionClientRpc(chosenSpawn);
        }
    }
}