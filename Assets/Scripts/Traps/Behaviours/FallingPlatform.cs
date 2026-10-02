using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using static UnityEngine.RuleTile.TilingRuleOutput;

public class FallingPlatform : ATrap
{
    #region [SerializeField] Private Attributs
    [SerializeField] private float _movementDuration = 2f;
    [SerializeField] private AnimationCurve _animationCurve;
    [SerializeField] float _direction = 1.0f;
    [SerializeField] float _coef = 0.01f;
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
    }

    override protected void FixedUpdate()
    {
        bool isCollideActive = (_state & TrapState.Collide) == TrapState.Collide;

        if (isCollideActive)
        {
            MovePlatformUpAndDown();
        }
    }

    private void MovePlatformUpAndDown()
    {
        if (elapsedTime < _movementDuration)
        {
            elapsedTime += Time.deltaTime;
            float strength = _animationCurve.Evaluate(elapsedTime / _movementDuration);

            //// Move the platform depending on the transform.rotation
            if (Mathf.Approximately(transform.rotation.eulerAngles.z, 0))
                transform.position = new Vector3(transform.position.x, transform.position.y + (strength * _coef) * _direction, transform.position.z);
            else if (Mathf.Approximately(transform.rotation.eulerAngles.z, 90))
                transform.position = new Vector3(transform.position.x - (strength * _coef) * _direction, transform.position.y, transform.position.z);
            else if (Mathf.Approximately(transform.rotation.eulerAngles.z, 180))
                transform.position = new Vector3(transform.position.x, transform.position.y - (strength * _coef) * _direction, transform.position.z);
            else if (Mathf.Approximately(transform.rotation.eulerAngles.z, 270))
                transform.position = new Vector3(transform.position.x + (strength * _coef) * _direction, transform.position.y, transform.position.z);
            else
            {
                transform.position = new Vector3(transform.position.x, transform.position.y - (strength * _coef) * _direction, transform.position.z);
                Debug.Log("FallingPlatform: MovePlatformUpAndDown: Invalid rotation");
            }
        }
        else
        {
            _direction *= -1.0f;
            elapsedTime = 0.0f;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!IsHost) return;

        bool isCollideActive = (_state & TrapState.Collide) == TrapState.Collide;

        if (!isCollideActive) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            PlaySoundServerRpc();
        }
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
