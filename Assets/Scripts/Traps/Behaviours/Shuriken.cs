using Unity.Netcode;
using UnityEngine;

public class Shuriken: ATrap
{
    [SerializeField] private AudioClip _AudioClip;

    override protected void Awake()
    {
        base.Awake();
        matrix = new int[,] {
            { 1 },
        };
    }

    override protected void FixedUpdate()
    {
        bool isCollideActive = (_state & TrapState.Collide) == TrapState.Collide;

        if (isCollideActive)
        {
            HandleShurikenBehaviour();
        }
    }

    private void HandleShurikenBehaviour()
    {
        return;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsHost) return;

        bool isCollideActive = (_state & TrapState.Collide) == TrapState.Collide;

        if (!isCollideActive) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            PlaySoundServerRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PlaySoundServerRpc()
    {
        PlaySoundClientRpc(transform.position);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlaySoundClientRpc(Vector3 position)
    {
        AudioSource.PlayClipAtPoint(_AudioClip, position);
    }
}
