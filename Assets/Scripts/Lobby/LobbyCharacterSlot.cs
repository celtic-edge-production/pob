using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// A place in the lobby scene where one character waits to be picked. The lobby scene holds these
// slots instead of the characters themselves: the server spawns the character prefabs from them
// (LobbyManager), only for the characters that do not exist yet. A character picked in the previous
// game comes back with its player, so it is never duplicated (no more PlayerCleaner).
public class LobbyCharacterSlot : MonoBehaviour
{
    [SerializeField] private NetworkObject _characterPrefab;

    public uint CharacterPrefabId => _characterPrefab != null ? _characterPrefab.PrefabIdHash : 0;

    // Server only. Spawns this slot's character unless one already exists (kept by its player).
    public void SpawnIfMissing()
    {
        if (_characterPrefab == null)
        {
            Debug.LogError($"[LobbyCharacterSlot] {name}: no character prefab assigned.", this);
            return;
        }

        foreach (PlayerCharacter character in FindObjectsByType<PlayerCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (character.IsSpawned && character.NetworkObject.PrefabIdHash == CharacterPrefabId)
                return;
        }

        NetworkObject spawned = Instantiate(_characterPrefab, transform.position, transform.rotation);
        spawned.name = _characterPrefab.name;

        // If its player leaves, the server gets it back instead of NGO destroying it (the lobby gives
        // it back to the pool, a game scene despawns it: PlayerIdentity.FreeCharacterOfLeavingOwner).
        spawned.DontDestroyWithOwner = true;

        // Unpicked characters go with the lobby. A picked one follows its player into the level
        // (PlayerCharacter.ApplyPersistence turns DestroyWithScene off while it is played).
        spawned.Spawn(destroyWithScene: true);
    }

    // Every peer: the slot a character belongs to, if this scene has one.
    public static LobbyCharacterSlot FindFor(NetworkObject character)
    {
        foreach (LobbyCharacterSlot slot in FindObjectsByType<LobbyCharacterSlot>(FindObjectsSortMode.None))
        {
            if (slot.CharacterPrefabId == character.PrefabIdHash)
                return slot;
        }
        return null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, new Vector3(1f, 2f, 0f));
    }

#if UNITY_EDITOR
    // Editor only: replaces the characters placed in the open scene by slots pointing to their
    // prefab, at the same position. Each character must be a prefab instance.
    [UnityEditor.MenuItem("Tools/PanicOnBoard/Replace lobby characters with slots")]
    private static void ReplaceCharactersWithSlots()
    {
        List<LobbyCharacter> characters = new List<LobbyCharacter>(
            FindObjectsByType<LobbyCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None));

        int replaced = 0;
        foreach (LobbyCharacter character in characters)
        {
            NetworkObject prefab = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(character.GetComponent<NetworkObject>());
            if (prefab == null)
            {
                Debug.LogError($"[LobbyCharacterSlot] {character.name} is not a prefab instance: make it a prefab first, then run this again.", character);
                continue;
            }

            GameObject slotObject = new GameObject($"Slot - {prefab.name}");
            UnityEditor.Undo.RegisterCreatedObjectUndo(slotObject, "Replace lobby characters with slots");
            slotObject.transform.SetPositionAndRotation(character.transform.position, character.transform.rotation);
            slotObject.transform.SetParent(character.transform.parent, true);

            LobbyCharacterSlot slot = slotObject.AddComponent<LobbyCharacterSlot>();
            slot._characterPrefab = prefab;

            UnityEditor.Undo.DestroyObjectImmediate(character.gameObject);
            replaced++;
        }

        Debug.Log($"[LobbyCharacterSlot] {replaced} character(s) replaced with slots. Save the scene.");
    }
#endif
}