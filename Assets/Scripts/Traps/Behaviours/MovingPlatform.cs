using UnityEngine;

public class MovingPlatform : ATrap
{
    [SerializeField] private float _movementDuration = 2f;
    [SerializeField] private AnimationCurve _animationCurve;
    [SerializeField] float _direction = 1.0f;
    [SerializeField] float _coef = 0.01f;

    private float elapsedTime = 0.0f;

    protected override void Awake()
    {
        base.Awake();
        matrix = new int[,] { { 1 } };
    }

    protected override void FixedUpdate()
    {
        // Server only: the server-owned ClientNetworkTransform replicates the rotation.
        if (!IsServer || !IsActive || _animationCurve == null)
            return;

        RotateLeftAndRight();
    }

    private void RotateLeftAndRight()
    {
        if (elapsedTime < _movementDuration)
        {
            elapsedTime += Time.deltaTime;
            float strength = _animationCurve.Evaluate(elapsedTime / _movementDuration);
            transform.rotation = new Quaternion(transform.rotation.x, transform.rotation.y,
                transform.rotation.z + strength * _coef * _direction, transform.rotation.w);
        }
        else
        {
            _direction *= -1.0f;
            elapsedTime = 0.0f;
        }
    }
}