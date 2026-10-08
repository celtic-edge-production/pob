using System.Collections;
using Unity.Netcode;
using UnityEngine;

// The chest spawned at the start of phase 1. Only plays its animations and sounds: the sequence
// is run by TrapSelection, which reads HasOpened / HasClosed (set by the animation events).
public class TrapChest : NetworkBehaviour
{
    [SerializeField] private AudioClip _OpenSoundAudioClip;
    [SerializeField] private AudioClip _CloseSoundAudioClip;

    private Animator _Animator;

    // Server only.
    public bool HasOpened { get; private set; }
    public bool HasClosed { get; private set; }

    private void Awake()
    {
        _Animator = GetComponent<Animator>();
    }

    private void Start()
    {
        transform.localPosition = Vector3.zero;
    }

    // Server only.
    public void PlayOpen()
    {
        HasOpened = false;
        _Animator.SetBool("Open", true);
    }

    // Server only.
    public void PlayClose()
    {
        HasClosed = false;
        _Animator.SetBool("Open", false);
    }

    // Server only: hides the chest, widens the camera for the placement, then despawns.
    public void Leave()
    {
        if (!IsServer)
            return;

        GetComponent<SpriteRenderer>().enabled = false;
        ChangeCameraClientRpc();
        StartCoroutine(DespawnAfterDelay());
    }

    // Animation event: end of the chest opening animation.
    public void OnChestOpen()
    {
        if (IsServer)
            HasOpened = true;
    }

    // Animation events still present in the clips (OpenChest is on the "OpenChest" animation);
    // the sequence no longer needs them, they are kept so the clips find a receiver.
    public void OpenChest()
    {
    }

    public void UiClosed()
    {
    }

    // Animation event: end of the chest closing animation.
    public void OnChestClose()
    {
        if (IsServer)
            HasClosed = true;
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void ChangeCameraClientRpc()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.TryGetComponent(out MainCameraController mainCameraController))
            mainCameraController.EnlargeCamera();
    }

    private IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(1.0f);

        if (IsSpawned)
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

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlaySoundClientRpc(bool isOpenSound, Vector3 position)
    {
        AudioSource.PlayClipAtPoint(isOpenSound ? _OpenSoundAudioClip : _CloseSoundAudioClip, position);
    }
}