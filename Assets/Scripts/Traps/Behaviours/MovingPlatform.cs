using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingPlatform : ATrap
{
    #region [SerializeField] Private Attributs
    [SerializeField] private float _movementDuration = 2f;
    [SerializeField] private AnimationCurve _animationCurve;
    [SerializeField] float _direction = 1.0f;
    [SerializeField] float _coef = 0.01f;
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
            RotatePlatformLeftAndRigth();
        }
    }

    private void RotatePlatformLeftAndRigth()
    {
        if (_animationCurve == null)
        {
            return;
        }

        if (elapsedTime < _movementDuration)
        {
            elapsedTime += Time.deltaTime;
            float strength = _animationCurve.Evaluate(elapsedTime / _movementDuration);
            transform.rotation = new Quaternion(transform.rotation.x, transform.rotation.y, transform.rotation.z + (strength * _coef) * _direction, transform.rotation.w);
        }
        else
        {
            _direction *= -1.0f;
            elapsedTime = 0.0f;
        }
    }
}
