using Unity.Netcode;
using UnityEngine;

public class BombProjectile : NetworkBehaviour
{
    private ulong ownerClientId;
    private Rigidbody2D rb;
    private bool isActive = true;

    [SerializeField]
    private Vector2 launchAngle = new Vector2(1, 3);

    private bool canBePickedUp = false;
    [SerializeField]
    private float pickUpDelay = 0.5f;
    [SerializeField]
    private float forceX = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void LaunchBomb(ulong ownerId, float force, int facingDirection)
    {
        //Debug.Log("Bomb launched by " + ownerId + " with force " + force + " and facing direction " + (facingDirection == 1 ? "right" : "left"));
        ownerClientId = ownerId;
        Vector2 rbVelocity = new Vector2(launchAngle.x * force * facingDirection * forceX, launchAngle.y * force);

        rb.linearVelocity = rbVelocity;
        Invoke(nameof(EnablePickUp), pickUpDelay); // Permet de ramasser après un délai
    }

    private void EnablePickUp()
    {
        canBePickedUp = true;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsServer || !IsSpawned || !isActive) return;

        if (collision.CompareTag("Player"))
        {
            var hitPlayer = collision.GetComponent<NetworkObject>();
            if (hitPlayer != null && hitPlayer.OwnerClientId != ownerClientId)
            {
                // Already running on the server: call the internal method directly.
                // Going through TransferBombServerRpc would validate the host as sender and
                // reject the transfer whenever a client threw the bomb.
                BombManager.Instance.AssignBombHolder(hitPlayer.OwnerClientId);
                DestroyBomb();
            }
            else if (hitPlayer != null && hitPlayer.OwnerClientId == ownerClientId && canBePickedUp)
            {
                PlayerCharacter playerController = hitPlayer.GetComponentInChildren<PlayerCharacter>();
                if (playerController != null)
                    playerController.hasBomb.Value = true;
                DestroyBomb();
            }
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void DestroyBomb()
    {
        // Several triggers can fire in the same physics step: only the first one may despawn.
        isActive = false;

        // Server-only (guarded in OnTriggerEnter2D). Despawn() also destroys the GameObject.
        NetworkObject.Despawn();
    }
}
