using Unity.Netcode;
using UnityEngine;

public class TransferManager : NetworkBehaviour
{
    public NetworkVariable<bool> IsAlive = new NetworkVariable<bool>(true);
    public GameObject holdBombIndicator;
    public Animator animator;
    public float invincibilityTime = 2f; // Temps d'invincibilit� apr�s avoir donn� la bombe
    private float currentInvincibilityTime = 0f; // Timer pour l'invincibilit�
    private bool isInvincible = false; // Statut d'invincibilit�

    public void MarkAsDead()
    {
        if (IsServer)
        {
            PlayerCharacter playerController = gameObject.GetComponent<PlayerCharacter>();
            if (playerController != null) {
                playerController.hasBomb.Value = false;
                Vector2 knockOutDirection = new Vector2(0, 1);

                //playerController.KnockOutClientRpc(knockOutDirection);
            }
            IsAlive.Value = false;
            animator.SetTrigger("isDeadGround");
            // Trigger death animation or effects here
        }
        DieClientRpc();
        //Debug.Log("You are dead!");
    }

    public void Revive()
    {
        PlayerCharacter playerController = gameObject.GetComponent<PlayerCharacter>();
        if (IsServer)
        {
            IsAlive.Value = true;
            playerController.hasBomb.Value = false;
            // Trigger revive animation or effects here
        }
        ReviveClientRpc();
    }

    void Update()
    {
        if (BombManager.Instance != null)
        {
            if (BombManager.Instance.IsBombHolder(OwnerClientId))
                holdBombIndicator.SetActive(true);
            else
                holdBombIndicator.SetActive(false);
        }
        if (isInvincible)
        {
            currentInvincibilityTime -= Time.deltaTime;
            if (currentInvincibilityTime <= 0f)
            {
                isInvincible = false; // Fin de l'invincibilit�
                //Debug.Log("Invincibility ended for player " + OwnerClientId);
            }
        }
    }

    // Appel�e quand le joueur transf�re la bombe
    public void OnBombTransferred()
    {
        isInvincible = true;
        currentInvincibilityTime = invincibilityTime; // R�initialise le timer
        //Debug.Log("Player " + OwnerClientId + " is now invincible for " + invincibilityTime + " seconds.");
    }

    // V�rifie si le joueur est invincible
    public bool IsPlayerInvincible()
    {
        return isInvincible;
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        PlayerCharacter playerController = gameObject.GetComponent<PlayerCharacter>();
        if (other.gameObject.CompareTag("Player") && IsAlive.Value && playerController.hasBomb.Value)
        {
            TransferManager otherTransferManager = other.gameObject.GetComponent<TransferManager>();
            if (otherTransferManager != null && (otherTransferManager.IsPlayerInvincible() || otherTransferManager.IsAlive.Value == false))
            {
                //Debug.LogWarning("- Player " + otherTransferManager.OwnerClientId + " is invincible and cannot receive the bomb.");
                return; // Le joueur ne peut pas recevoir
            }
            // Exemple depuis PlayerManager ou GameRoundManager
            if (BombManager.Instance != null)
            {
                //Debug.Log("BombManager trouv�, v�rification du porteur...");
                if (BombManager.Instance.IsBombHolder(OwnerClientId))
                {
                    //Debug.Log("C'est bien le porteur de la bombe !");
                    BombManager.Instance.TransferBombServerRpc(other.gameObject.GetComponent<NetworkObject>().OwnerClientId);
                }
                else
                {
                    //Debug.Log("Le joueur n'est pas le porteur de la bombe.");
                }
            }
            else
            {
                Debug.LogWarning("BombManager est null !");
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void DieClientRpc()
    {
        // Trigger death animation or effects here
        //Debug.Log("You are dead!");
        animator.SetTrigger("isDeadGround");
        holdBombIndicator.SetActive(false);
        PlayerCharacter playerController = gameObject.GetComponent<PlayerCharacter>();
        playerController.DesactivateCamera();
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void ReviveClientRpc()
    {
        // Trigger win animation or effects here
        animator.ResetTrigger("isDeadGround");
        animator.Play("Idle");
        holdBombIndicator.SetActive(false);
    }
}
