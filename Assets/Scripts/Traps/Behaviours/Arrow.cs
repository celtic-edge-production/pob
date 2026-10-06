using Unity.Netcode;
using UnityEngine;

public class Arrow : NetworkBehaviour
{
    [SerializeField] private AudioClip _HitPlayerAudioClip;
    [SerializeField] private AudioClip _HitObjectAudioClip;

    private bool _hasHit;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Several triggers can fire in the same physics step: only the first one may despawn.
        if (!IsServer || !IsSpawned || _hasHit)
            return;

        // Ignore the shooter that fired this arrow.
        if ((collision.includeLayers.value & LayerMask.GetMask("Shooter")) != 0)
            return;

        _hasHit = true;
        PlaySoundClientRpc(collision.gameObject.CompareTag("Player"), transform.position);
        NetworkObject.Despawn(true);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlaySoundClientRpc(bool hitPlayer, Vector3 position)
    {
        // Choose the appropriate sound to play
        var clip = hitPlayer ? _HitPlayerAudioClip : _HitObjectAudioClip;
        AudioSource.PlayClipAtPoint(clip, position);
    }
}
