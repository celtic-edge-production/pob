using Unity.Netcode;
using UnityEngine;

public class Firebox : ATrap
{
    [SerializeField] private float _durationBeforeFiring = 4f;
    [SerializeField] private GameObject childFire;

    // Server only: time since the last shot.
    private float _elapsedTime;

    protected override void Awake()
    {
        base.Awake();
        matrix = new int[,] {
            { 1 },
        };

        ResetToIdle();
    }

    private void OnEnable()
    {
        ResetToIdle();
    }

    protected override void FixedUpdate()
    {
        if (!IsActive)
        {
            // Deactivated in the middle of a shot (end of phase 2): no flame left burning.
            if (childFire.activeSelf)
                ResetToIdle();
            return;
        }

        // The server keeps the rhythm and starts each shot on every peer, so the flame
        // exists everywhere: players detect the hit on their own machine (PlayerCharacter.TrapPunch).
        if (IsServer)
            HandleFirebox();
    }

    // Server only.
    private void HandleFirebox()
    {
        if (_elapsedTime < _durationBeforeFiring)
        {
            _elapsedTime += Time.fixedDeltaTime;
            return;
        }

        _elapsedTime = 0f;

        if (_animator.GetBool("IsIdle"))
            StartReloadClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void StartReloadClientRpc()
    {
        _animator.SetBool("IsIdle", false);
        _animator.SetBool("IsReloading", true);
    }

    // Animation event: runs on every peer playing the reload animation.
    public void OnReloadComplete()
    {
        _animator.SetBool("IsReloading", false);
        _animator.SetBool("IsFiring", true);
        childFire.SetActive(true);

        // PlayActionSound already broadcasts to everyone: sent once, by the server.
        if (IsServer)
            PlayActionSound();
    }

    // Animation event: runs on every peer playing the firing animation.
    public void OnFiringComplete()
    {
        ResetToIdle();
    }

    private void ResetToIdle()
    {
        _animator.SetBool("IsIdle", true);
        _animator.SetBool("IsFiring", false);
        _animator.SetBool("IsReloading", false);

        if (childFire != null)
            childFire.SetActive(false);
    }
}