using System.Collections;
using Unity.Netcode;
using UnityEngine;

// The chest spawned at the start of phase 1. Its animation events drive the chest UI.
public class TrapChest : NetworkBehaviour
{
    // The chest currently spawned, if any (one per phase 1).
    public static TrapChest Current { get; private set; }

    public GameObject Image;

    [SerializeField] private AudioClip _OpenSoundAudioClip;
    [SerializeField] private AudioClip _CloseSoundAudioClip;

    private Animator _Animator;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Current = this;
    }

    public override void OnNetworkDespawn()
    {
        if (Current == this)
            Current = null;

        base.OnNetworkDespawn();
    }

    private void Start()
    {
        _Animator = GetComponent<Animator>();

        // The chest UI lives in the level scene and starts inactive: find it by type, inactive included.
        ChestUi chestUi = FindFirstObjectByType<ChestUi>(FindObjectsInactive.Include);
        if (chestUi != null)
            Image = chestUi.gameObject;
        else
            Debug.LogError("[TrapChest] No ChestUi found in the scene.");

        TrapPlacementManager.Instance.HideAllPlacedTraps();

        transform.localPosition = Vector3.zero;
    }

    // Server only (called by Phase1).
    public void OpenChest()
    {
        if (!IsServer)
            return;

        _Animator.SetBool("Open", true);
    }

    // Animation event: end of the chest opening animation.
    public void OnChestOpen()
    {
        if (!IsServer)
            return;

        Image.SetActive(true);

        // The traps are spawned at the end of the UI opening animation (ChestUi.OnUIOpen).
        Animator uiAnimator = Image.GetComponent<Animator>();
        uiAnimator.SetBool("IsToggle", true);
        uiAnimator.SetBool("IsOpening", true);
    }

    // Animation event: end of the UI closing animation.
    public void UiClosed()
    {
        if (!IsServer)
            return;

        _Animator.SetBool("Open", false);
    }

    // Animation event: end of the chest closing animation.
    public void OnChestClose()
    {
        if (!IsServer)
            return;

        GetComponent<SpriteRenderer>().enabled = false;
        ChangeCameraClientRpc();
        StartCoroutine(DespawnAfterDelay());
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ChangeCameraClientRpc()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.TryGetComponent(out MainCameraController mainCameraController))
            mainCameraController.EnlargeCamera();
    }

    private IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(1.0f);
        NetworkObject.Despawn(true);
    }

    // Animation event.
    private void OpenChestSound() => PlaySound(true);

    // Animation event.
    private void CloseChestSound() => PlaySound(false);

    private void PlaySound(bool isOpenSound)
    {
        if (IsServer)
            PlaySoundClientRpc(isOpenSound, transform.position);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlaySoundClientRpc(bool isOpenSound, Vector3 position)
    {
        AudioSource.PlayClipAtPoint(isOpenSound ? _OpenSoundAudioClip : _CloseSoundAudioClip, position);
    }
}