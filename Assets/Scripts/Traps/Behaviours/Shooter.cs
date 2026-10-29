using Unity.Netcode;
using UnityEngine;

public class Shooter : ATrap
{
    [SerializeField] private float _DurationBeforeFiring = 6f;
    [SerializeField] private float _ShootSpeed = 4f;
    [SerializeField] private GameObject _Arrow;

    private float _ElapsedTime = 0.0f;

    protected override void Awake()
    {
        base.Awake();
        matrix = new int[,] { { 1 } };
    }

    protected override void FixedUpdate()
    {
        if (!IsServer || !IsActive)
            return;

        if (_ElapsedTime < _DurationBeforeFiring)
        {
            _ElapsedTime += Time.deltaTime;
            return;
        }

        _ElapsedTime = 0.0f;
        _animator.SetBool("Shoot", true);
        PlayActionSound();
    }

    // Animation event: only the server spawns the arrow.
    public void ShootArrow()
    {
        if (IsServer)
            SpawnArrow();
    }

    // Animation event: end of the shooting animation.
    public void OnArrowShot()
    {
        _animator.SetBool("Shoot", false);
    }

    private void SpawnArrow()
    {
        GameObject arrow = Instantiate(_Arrow, transform.position, transform.rotation);

        NetworkObject networkObject = arrow.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Destroy(arrow);
            return;
        }

        // Its hits are credited to whoever placed this shooter.
        if (arrow.TryGetComponent(out Arrow arrowComponent))
            arrowComponent.TrapOwnerId = ChosenBy;

        networkObject.Spawn(destroyWithScene: true); // Belongs to the level: never carried into the next scene.
        arrow.GetComponent<Rigidbody2D>().linearVelocity = GetShootDirection() * _ShootSpeed;
    }

    // Down by default, then following the shooter's rotation in 90 degree steps.
    private Vector2 GetShootDirection()
    {
        float z = transform.eulerAngles.z;

        if (Mathf.Approximately(z, 90)) return Vector2.right;
        if (Mathf.Approximately(z, 180)) return Vector2.up;
        if (Mathf.Approximately(z, 270)) return Vector2.left;
        return Vector2.down;
    }
}
