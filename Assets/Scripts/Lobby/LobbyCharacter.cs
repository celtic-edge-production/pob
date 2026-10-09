using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// Lobby behaviour of a character: going back to its slot when it is released.
// Characters are spawned from LobbyCharacterSlot; the slot gives the home position, on every peer.
[RequireComponent(typeof(NetworkObject))]
public class LobbyCharacter : MonoBehaviour
{
    private NetworkObject _networkObject;

    private void Awake()
    {
        _networkObject = GetComponent<NetworkObject>();

        // Spawned instances are named "X(Clone)" on the clients: keep the prefab name everywhere
        // (the end-of-game message shows it).
        name = name.Replace("(Clone)", "").Trim();
    }

    // Puts the character back on its lobby slot. Does nothing outside the lobby (no slot there).
    // Only the peer with authority over its transform can move it; the others are no-ops.
    public void ResetToOriginalPosition()
    {
        LobbyCharacterSlot slot = LobbyCharacterSlot.FindFor(_networkObject);
        if (slot == null)
            return;

        Vector3 home = slot.transform.position;
        home.z = transform.position.z;

        if ((transform.position - home).sqrMagnitude < 0.0001f)
            return;

        // A teleport, not a move: the others would see the character slide across the lobby, and its
        // copy would shove every player on the way (see PlayerCharacter.ResetPositionClientRpc).
        if (_networkObject.IsSpawned && TryGetComponent(out NetworkTransform networkTransform))
        {
            if (networkTransform.CanCommitToTransform)
                networkTransform.Teleport(home, transform.rotation, transform.localScale);
        }
        else
        {
            transform.position = home;
        }
    }
}