using Unity.Netcode;
using UnityEngine;

public class Arrow : NetworkBehaviour
{
    [SerializeField] private AudioClip _HitPlayerAudioClip;
    [SerializeField] private AudioClip _HitObjectAudioClip;

    [Rpc(SendTo.Server)]
    private void DestroyArrowServerRpc()
    {
        NetworkObject networkObject = GetComponent<NetworkObject>();

        if (networkObject != null)
            networkObject.Despawn(true);
        else
            Destroy(gameObject);

        Debug.Log("Arrow must be destroyed");
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsHost) return;

        Debug.Log("Arrow collision with " + collision.gameObject.name);

        LayerMask trapStunLayer = LayerMask.GetMask("Shooter");

        Debug.Log("Arrow collision with " + collision.gameObject.layer + " compared to : " + trapStunLayer.value);

        // If the arrow is not colliding an object that don't come from shooter
        if ((collision.includeLayers.value & LayerMask.GetMask("Shooter")) == 0)
        {
            // Check if collision is with a player tag
            if (collision.gameObject.CompareTag("Player"))
            {
                // Play the sound of the arrow hitting the player
                PlaySoundServerRpc(true);
                //SoundManagerSO.PlaySoundFXClip(_HitPlayerAudioClip, transform.position, 1.0f);
            }
            else
            {
                PlaySoundServerRpc(false);
                //SoundManagerSO.PlaySoundFXClip(_HitObjectAudioClip, transform.position, 1.0f);
            }
            DestroyArrowServerRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PlaySoundServerRpc(bool IsHitPlayer)
    {
        PlaySoundClientRpc(IsHitPlayer, transform.position);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlaySoundClientRpc(bool hitPlayer, Vector3 position)
    {
        // Choose the appropriate sound to play
        var clip = hitPlayer ? _HitPlayerAudioClip : _HitObjectAudioClip;
        AudioSource.PlayClipAtPoint(clip, position);
    }
}
