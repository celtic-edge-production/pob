using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class BombTrap: ATrap
{
    [SerializeField] private AudioClip _AudioClip;

    #region Public Attributs
    public bool isExploding = false;
    #endregion

    #region Private Attributs
    private Vector3Int _targetCell;
    #endregion

    override protected void Awake()
    {
        base.Awake();
        matrix = new int[,] {
            { 1 },
        };
        isBombTrap = true;
    }

    override protected void FixedUpdate()
    {
        bool isCollideActive = (_state & TrapState.Collide) == TrapState.Collide;

        if (isCollideActive)
        {
            return;
        }
    }

    public void ExplodeBomb(Vector3Int targetCell)
    {
        _targetCell = targetCell;

        if (!isExploding)
        {
            isExploding = true;
            StartCoroutine(Explode());
        }
    }

    // Animation event: runs on every peer playing the animation, only the server removes the trap.
    public void OnExplode()
    {
        if (!IsServer)
            return;

        TrapPlacementManager.Instance.RemoveTrapAt(_targetCell);
    }

    private IEnumerator Explode()
    {
        yield return new WaitForSeconds(1.0f);
        _animator.SetBool("IsExploding", true);
    }

    public void OnExplosionComplete()
    {
        if (!IsHost) return;

        // Meme correctif que dans Arrow.cs : Despawn() plutot que Destroy() direct
        // sur un objet reseau spawn (confirme via le prefab Bomb.prefab).
        Debug.Log("Explosion Complete");

        NetworkObject networkObject = GetComponent<NetworkObject>();

        if (networkObject != null)
            networkObject.Despawn(true);
        else
            Destroy(gameObject);
    }

    private void PlaySound()
    {
        if (!IsHost) return;

        PlaySoundClientRpc(transform.position);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlaySoundClientRpc(Vector3 position)
    {
        AudioSource.PlayClipAtPoint(_AudioClip, position);
    }
}
