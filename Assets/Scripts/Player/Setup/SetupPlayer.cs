using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class SetupPlayer : NetworkBehaviour
{

    [Header("Player Controller to Setup")]
    public PlayerIdentity AU_Controller;

    [Header("NetworkObject to Spawn")]
    public GameObject Cursor;
    public GameObject Data;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            StartCoroutine(WaitBeforeInitialize());
    }

    private IEnumerator WaitBeforeInitialize()
    {
        yield return null;

        const float timeout = 5f;
        float elapsed = 0f;

        while (!IsSpawned && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        if (!IsSpawned)
        {
            yield break;
        }

        InitializePlayerServerRpc();
    }

    [Rpc(SendTo.Server)]
    private void InitializePlayerServerRpc()
    {
        Transform playerObjectTransform = NetworkManager.Singleton.ConnectedClients[OwnerClientId].PlayerObject.transform;

        Data = Instantiate(Data);
        // Must be destroyed with the owner (see PlayerIdentity.OnNetworkSpawn).
        Data.GetComponent<NetworkObject>().DontDestroyWithOwner = false;
        Data.GetComponent<NetworkObject>().SpawnWithOwnership(OwnerClientId);
        Data.transform.SetParent(playerObjectTransform);

        Cursor = Instantiate(Cursor);
        Cursor.GetComponent<NetworkObject>().DontDestroyWithOwner = false;
        Cursor.GetComponent<NetworkObject>().SpawnWithOwnership(OwnerClientId);
        Cursor.transform.SetParent(playerObjectTransform);

        InitializePlayerClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void InitializePlayerClientRpc()
    {
        if (!IsOwner)
            return;

        foreach (Transform child in NetworkManager.Singleton.ConnectedClients[OwnerClientId].PlayerObject.transform)
        {
            if (child.gameObject.name.Contains("Cursor"))
            {
                Cursor = child.gameObject;
            }
            else if (child.gameObject.name.Contains("Data"))
            {
                Data = child.gameObject;
            }
        }

        AU_Controller.Init();
        AU_Controller.SetPlayerID(OwnerClientId + 1);
        AU_Controller.UpdatePlayerData();
    }
}