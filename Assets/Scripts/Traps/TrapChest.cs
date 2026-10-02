using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class TrapChest : NetworkBehaviour
{
    public GameObject Image;

    [SerializeField] private AudioClip _OpenSoundAudioClip;
    [SerializeField] private AudioClip _CloseSoundAudioClip;

    private Animator _Animator;
    private GameObject _UiChest;

    private void Start()
    {
        _Animator = GetComponent<Animator>();

        GetChestUiFromCanvas();

        // Place the chest
        transform.localPosition = new Vector3(0, 0, 0);
    }

    private void GetChestUiFromCanvas()
    {
        // Find "ChestUi" gameObject in child of canvas
        foreach (Transform child in GameObject.Find("ChestUiCanva").transform)
        {
            if (child.gameObject.name.Contains("ChestUi"))
            {
                Image = child.gameObject;
            }
        }
        TrapPlacementManager.Instance.HideAllPlacedTrapServerRpc();
    }

    public void OpenChest()
    {
        if (!IsOwner) return;

        Debug.Log("OpenChest");

        // Play animation open chest
        _Animator.SetBool("Open", true);
    }

    /*
     * @brief Called when the chest is opened at the end of the animation
     */
    public void OnChestOpen()
    {
        if (!IsOwner) return;

        Debug.Log("Chest Opened");

        // Activate UI and Open UI
        OpenUI();
    }

    public void OpenUI()
    {
        if (!IsOwner) return;

        Image.SetActive(true);
        Image.GetComponent<Animator>().SetBool("IsToggle", true);
        Image.GetComponent<Animator>().SetBool("IsOpening", true);

        // Traps are spawned in the UI after the open animation
    }

    /*
     * @brief Called when the UI is Closed at the end of the animation
     */
    public void UiClosed()
    {
        if (!IsOwner) return;

        Debug.Log("UI Closed");
        // TODO: Despawn Traps not used

        // Play animation Close chest
        _Animator.SetBool("Open", false);
    }


    public void OnChestClose()
    {
        if (!IsOwner) return;

        GetComponent<SpriteRenderer>().enabled = false;

        ChangeCameraClientRpc();

        StartCoroutine(DestroyChest());
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void ChangeCameraClientRpc()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            MainCameraController mainCameraController = mainCamera.GetComponent<MainCameraController>();
            if (mainCameraController != null)
            {
                mainCameraController.EnlargeCamera();
            }
        }
    }

    private IEnumerator DestroyChest()
    {
        yield return new WaitForSeconds(1.0f);

        // Meme correctif que Arrow.cs/BombTrap.cs : Despawn() plutot que Destroy()
        // direct sur un objet reseau spawn (confirme via Phase1.cs : networkObject.Spawn()).
        NetworkObject networkObject = GetComponent<NetworkObject>();

        if (networkObject != null)
            networkObject.Despawn(true);
        else
            Destroy(gameObject);
    }


    private void CloseChestSound()
    {
        PlaySound(false);
    }

    private void OpenChestSound()
    {
        PlaySound(true);
    }

    private void PlaySound(bool isOpenSound)
    {
        if (!IsHost) return;

        PlaySoundClientRpc(isOpenSound, transform.position);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlaySoundClientRpc(bool isOpenSound, Vector3 position)
    {
        Debug.Log("PlaySoundClientRpc");
        if (isOpenSound)
        {
            AudioSource.PlayClipAtPoint(_OpenSoundAudioClip, position);
        }
        else
        {
            AudioSource.PlayClipAtPoint(_CloseSoundAudioClip, position);
        }

    }
}
