using System.Collections;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class Shooter: ATrap
{
    #region [SerializeField] Private Attributs
    [SerializeField] private float _DurationBeforeFiring = 6f;
    [SerializeField] private float _ShootSpeed = 4f;
    [SerializeField] private GameObject _Arrow;
    [SerializeField] private AudioClip _audioClip;
    #endregion

    #region private Attributs
    private float _ElapsedTime = 0.0f;
    private Arrow[] _Arrows;
    #endregion

    override protected void Awake()
    {
        base.Awake();
        matrix = new int[,] {
            { 1 },
        };
    }

    override protected void FixedUpdate()
    {
        if (!IsHost) return;

        bool isCollideActive = (_state & TrapState.Collide) == TrapState.Collide;

        if (isCollideActive)
        {
            HandleShooter();
        }
    }

    private void HandleShooter()
    {
        if (_ElapsedTime < _DurationBeforeFiring)
        {
            _ElapsedTime += Time.deltaTime;
        }
        else
        {
            StartShootArrow();
            _ElapsedTime = 0.0f;
        }
    }

    private void StartShootArrow()
    {
        _animator.SetBool("Shoot", true);
        Debug.Log("Start shoot arrow");
        PlaySoundServerRpc();
        //SoundManagerSO.PlaySoundFXClip(_audioClip, transform.position, 1.0f);
    }

    public void ShootArrow()
    {
        if (!IsServer)
            return;

        SpawnArrow();
    }

    private void SpawnArrow()
    {
        GameObject arrow = Instantiate(_Arrow, transform.position, Quaternion.identity);

        // instantiate the arrow
        NetworkObject networkObject = arrow.GetComponent<NetworkObject>();

        if (networkObject != null)
        {
            networkObject.Spawn();
        }
        else
        {
            return;
        }

        // Add velocity to the arrow
        float rotationZ = transform.eulerAngles.z;

        Debug.Log("Shooter shoot arrow RotationZ: " + rotationZ);

        // Calculate the direction based on the shooter's rotation
        Vector2 shootDirection = Vector2.down; // Default is down

        if (Mathf.Approximately(rotationZ, 0))
        {
            // Default orientation (down)
            shootDirection = Vector2.down;
        }
        else if (Mathf.Approximately(rotationZ, 90))
        {
            // Rotated to the right
            shootDirection = Vector2.right;
        }
        else if (Mathf.Approximately(rotationZ, 180))
        {
            // Rotated upward
            shootDirection = Vector2.up;
        }
        else if (Mathf.Approximately(rotationZ, 270))
        {
            // Rotated to the left
            shootDirection = Vector2.left;
        }

        // rotate the arrow same as the shooter
        arrow.transform.rotation = transform.rotation;
        arrow.GetComponent<Rigidbody2D>().linearVelocity = shootDirection * _ShootSpeed;

        Debug.Log("Arrow shot");
    }

    public void OnArrowShooted()
    {
        _animator.SetBool("Shoot", false);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PlaySoundServerRpc()
    {
        if (!IsHost) return;

        PlaySoundClientRpc(transform.position);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlaySoundClientRpc(Vector3 position)
    {
        AudioSource.PlayClipAtPoint(_audioClip, position);
    }
}
