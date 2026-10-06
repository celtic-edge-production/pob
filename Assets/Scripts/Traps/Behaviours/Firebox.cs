using UnityEngine;

public class Firebox : ATrap
{
    [SerializeField] private float _durationBeforeFiring = 4f;
    [SerializeField] private GameObject childFire;

    private float elapsedTime = 0.0f;

    protected override void Awake()
    {
        base.Awake();
        matrix = new int[,] { { 1 } };
        ResetAnimation();
    }

    private void OnEnable()
    {
        ResetAnimation();
    }

    private void ResetAnimation()
    {
        _animator.SetBool("IsIdle", true);
        _animator.SetBool("IsFiring", false);
        _animator.SetBool("IsReloading", false);
    }

    protected override void FixedUpdate()
    {
        if (!IsActive)
            return;

        if (elapsedTime < _durationBeforeFiring)
        {
            elapsedTime += Time.deltaTime;
            return;
        }

        elapsedTime = 0.0f;

        if (_animator.GetBool("IsIdle"))
        {
            _animator.SetBool("IsIdle", false);
            _animator.SetBool("IsReloading", true);
        }
    }

    // Animation event: end of the reload animation.
    public void OnReloadComplete()
    {
        if (!IsServer)
            return;

        _animator.SetBool("IsReloading", false);
        _animator.SetBool("IsFiring", true);
        childFire.SetActive(true);
        PlayActionSound();
    }

    // Animation event: end of the firing animation.
    public void OnFiringComplete()
    {
        _animator.SetBool("IsFiring", false);
        childFire.SetActive(false);
        _animator.SetBool("IsIdle", true);
    }
}