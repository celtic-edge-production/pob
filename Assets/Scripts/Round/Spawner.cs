using UnityEngine;
using System.Collections.Generic;

public class Spawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    public Vector3[] spawnPoints;

    // Server only. Each owner teleports its own character, which must be active to do so:
    // call it while the characters are visible.
    public void ResetCharactersPosition()
    {
        List<PlayerCharacter> players = new List<PlayerCharacter>(Players.Characters);

        if (spawnPoints.Length < players.Count)
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