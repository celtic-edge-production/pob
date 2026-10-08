using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class BombTrap : ATrap
{
    [Tooltip("Seconds between the explosion and the removal of the bomb (length of the explosion animation).")]
    [SerializeField] private float _explosionDuration = 1f;

    private bool _hasExploded;

    protected override void Awake()
    {
        base.Awake();
        matrix = new int[,] { { 1 } };
        isBombTrap = true;
    }

    // Server only (TrapPlacementManager.PlaceTrap). Explodes as soon as it is placed, even in
    // phase 1: the cell is free right away and another player can place a trap there.
    public void ExplodeBomb(Vector3Int targetCell)
    {
        if (!IsServer || _hasExploded)
            return;

        _hasExploded = true;

        TrapPlacementManager.Instance.RemoveTrapAt(targetCell);
        PlayExplosionClientRpc();
        StartCoroutine(DespawnAfterExplosion());
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlayExplosionClientRpc()
    {
        // Trap animations are off in phase 1; this one is the feedback of the placement, so it is forced on.
        _animator.enabled = true;
        _animator.SetBool("IsExploding", true);
    }

    // Server only. Timed here rather than by an animation event, so the removal never depends on
    // whether the animator is running.
    private IEnumerator DespawnAfterExplosion()
    {
        yield return new WaitForSeconds(_explosionDuration);

        if (IsSpawned)
            NetworkObject.Despawn(true);
    }

    // Animation events still present in the explosion clip. The gameplay no longer waits for them
    // (see ExplodeBomb and DespawnAfterExplosion); they are kept so the clip finds a receiver.
    public void OnExplode()
    {
    }

    public void OnExplosionComplete()
    {
    }

    // Animation event.
    private void PlaySound()
    {
        PlayActionSound();
    }
}