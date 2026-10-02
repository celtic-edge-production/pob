using Unity.Netcode;
using UnityEngine;

public class Firebox : ATrap
{
    #region [SerializeField] Private Attributs
    [SerializeField] private float _durationBeforeFiring = 4f;
    [SerializeField] private GameObject childFire;
    [SerializeField] private AudioClip _AudioClip;
    #endregion

    #region private Attributs
    private float elapsedTime = 0.0f;
    #endregion

    override protected void Awake()
    {
        base.Awake();
        matrix = new int[,] {
            { 1 },
        };

        _animator.SetBool("IsIdle", true);
        _animator.SetBool("IsFiring", false);
        _animator.SetBool("IsReloading", false);
    }

    private void OnEnable()
    {
        _animator.SetBool("IsIdle", true);
        _animator.SetBool("IsFiring", false);
        _animator.SetBool("IsReloading", false);
    }

    override protected void FixedUpdate()
    {
        bool isCollideActive = (_state & TrapState.Collide) == TrapState.Collide;

        if (isCollideActive)
        {
            HandleFirebox();
        }
    }

    public void OnReloadComplete()
    {
        if (!IsHost) return;

        _animator.SetBool("IsReloading", false);
        _animator.SetBool("IsFiring", true);
        childFire.SetActive(true);
        PlaySoundServerRpc();
    }

    public void OnFiringComplete()
    {
        _animator.SetBool("IsFiring", false);
        childFire.SetActive(false);
        _animator.SetBool("IsIdle", true);
    }

    private void HandleFirebox()
    {
        if (elapsedTime < _durationBeforeFiring)
        {
            elapsedTime += Time.deltaTime;
        }
        else
        {
            // Check if the firebow is idle or not
            if (_animator.GetBool("IsIdle")) {
                // Toggle Firing Animation
                _animator.SetBool("IsIdle", false);
                _animator.SetBool("IsReloading", true);
                Debug.Log("Firebox is firing");
            }
            elapsedTime = 0.0f;
        }
        return;
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
