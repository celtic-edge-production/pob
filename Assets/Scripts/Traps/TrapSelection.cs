using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

// Server only: the whole chest sequence of phase 1, in one place and in order.
// TrapChest and ChestUi only play their animations and report, through their animation events,
// when they are done; a missing event never blocks the game (timeout).
public class TrapSelection : MonoBehaviour
{
    [Tooltip("Seconds between the start of phase 1 and the chest opening.")]
    [SerializeField] private float _delayBeforeOpening = 3f;

    [Tooltip("Longest wait for an animation before going on anyway (animation event missing or renamed).")]
    [SerializeField] private float _animationTimeout = 5f;

    private Coroutine _sequence;

    public void Begin(GameObject chestPrefab)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        // Never two chests at once.
        if (_sequence != null)
            StopCoroutine(_sequence);

        _sequence = StartCoroutine(RunSequence(chestPrefab));
    }

    // Server only: the game ends before the chest has finished (not enough players).
    public void Stop()
    {
        if (_sequence != null)
            StopCoroutine(_sequence);

        _sequence = null;
    }

    // False once the session stops (quit, disconnect, back to the lobby) or the chest is gone:
    // the sequence must not send anything any more.
    private static bool IsSessionRunning(TrapChest chest)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        return networkManager != null && networkManager.IsListening && !networkManager.ShutdownInProgress
            && chest != null && chest.IsSpawned;
    }

    private IEnumerator RunSequence(GameObject chestPrefab)
    {
        TrapPlacementManager traps = TrapPlacementManager.Instance;

        // The chest UI lives in the level scene and starts inactive.
        ChestUi chestUi = FindFirstObjectByType<ChestUi>(FindObjectsInactive.Include);
        if (chestUi == null)
        {
            Debug.LogError("[TrapSelection] No ChestUi found in the scene.");
            yield break;
        }

        TrapChest chest = SpawnChest(chestPrefab);
        if (chest == null)
            yield break;

        // The placed traps are hidden while the chest offers new ones.
        traps.HideAllPlacedTraps();

        yield return new WaitForSeconds(_delayBeforeOpening);
        if (!IsSessionRunning(chest))
            yield break;

        // 1. The chest opens, then its UI.
        chest.PlayOpen();
        yield return WaitForAnimation(() => chest.HasOpened || !IsSessionRunning(chest), "chest opening");
        if (!IsSessionRunning(chest))
            yield break;

        chestUi.PlayOpen();
        yield return WaitForAnimation(() => chestUi.HasOpened || !IsSessionRunning(chest), "chest UI opening");
        if (!IsSessionRunning(chest))
            yield break;

        // 2. One trap per player, then wait until every player still here has chosen.
        traps.CreateTrapsForSelection(chestUi.GetComponent<RectTransform>());
        yield return new WaitUntil(() => traps.EveryoneHasChosen || !IsSessionRunning(chest));
        if (!IsSessionRunning(chest))
            yield break;

        // 3. The UI closes and the placed traps come back; the chosen traps can now be placed.
        traps.ShowAllPlacedTraps();
        chestUi.PlayClose();
        yield return WaitForAnimation(() => chestUi.HasClosed || !IsSessionRunning(chest), "chest UI closing");
        if (!IsSessionRunning(chest))
            yield break;

        chestUi.EndClose();
        traps.MakeChosenTrapsPlaceable();

        // 4. The chest closes and leaves.
        chest.PlayClose();
        yield return WaitForAnimation(() => chest.HasClosed || !IsSessionRunning(chest), "chest closing");
        if (!IsSessionRunning(chest))
            yield break;

        chest.Leave();
        _sequence = null;
    }

    private static TrapChest SpawnChest(GameObject chestPrefab)
    {
        GameObject chestObject = Instantiate(chestPrefab);
        chestObject.transform.position = Vector3.zero;

        if (!chestObject.TryGetComponent(out NetworkObject networkObject) || !chestObject.TryGetComponent(out TrapChest chest))
        {
            Debug.LogError("[TrapSelection] The chest prefab needs a NetworkObject and a TrapChest.");
            Destroy(chestObject);
            return null;
        }

        // Spawn() keeps the object across scene loads by default: a game ending during the chest
        // (not enough players) brought it into the lobby. It belongs to the level.
        networkObject.Spawn(destroyWithScene: true);
        return chest;
    }

    private IEnumerator WaitForAnimation(Func<bool> isDone, string step)
    {
        float giveUpTime = Time.time + _animationTimeout;

        while (!isDone() && Time.time < giveUpTime)
            yield return null;

        if (!isDone())
            Debug.LogWarning($"[TrapSelection] No end event for the {step} animation after {_animationTimeout}s, going on.");
    }
}