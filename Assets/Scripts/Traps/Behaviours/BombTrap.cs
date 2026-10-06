using System.Collections;
using UnityEngine;

public class BombTrap : ATrap
{
    public bool isExploding = false;

    private Vector3Int _targetCell;

    protected override void Awake()
    {
        base.Awake();
        matrix = new int[,] { { 1 } };
        isBombTrap = true;
    }

    // Server only (TrapPlacementManager.PlaceTrap).
    public void ExplodeBomb(Vector3Int targetCell)
    {
        _targetCell = targetCell;

        if (!isExploding)
        {
            isExploding = true;
            StartCoroutine(Explode());
        }
    }

    private IEnumerator Explode()
    {
        yield return new WaitForSeconds(1.0f);
        _animator.SetBool("IsExploding", true);
    }

    // Animation event: runs on every peer playing the animation, only the server removes the trap.
    public void OnExplode()
    {
        if (IsServer)
            TrapPlacementManager.Instance.RemoveTrapAt(_targetCell);
    }

    // Animation event: end of the explosion.
    public void OnExplosionComplete()
    {
        if (IsServer)
            NetworkObject.Despawn(true);
    }

    // Animation event.
    private void PlaySound()
    {
        PlayActionSound();
    }
}