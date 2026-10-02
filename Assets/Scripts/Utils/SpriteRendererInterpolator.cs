using UnityEngine;

public class SpriteRendererInterpolator : MonoBehaviour
{
    //  Fields ----------------------------------------
    [SerializeField]
    private SpriteRenderer _spriteRenderer;

    private float _lastTransformPositionX;

    public bool needDefaultFlip = false;

    private float _currentVelocityX;

    //  Unity Methods ---------------------------------
    protected void Start()
    {
        _lastTransformPositionX = transform.position.x;
        _currentVelocityX = 0;
    }

    protected void FixedUpdate()
    {
        // Since movement is done on FixedUpdate,
        // Calculate velocity on FixedUpdate too
        _currentVelocityX = transform.position.x - _lastTransformPositionX;

        // Face direction of movement
        //if (_currentVelocityX != 0)
        //{
        //    bool isFlipX = _currentVelocityX > 0;
        //    _spriteRenderer.flipX = isFlipX;
        //}
        if (_currentVelocityX > 0)
        {
            _spriteRenderer.flipX = needDefaultFlip;
        }
        else if (_currentVelocityX < 0)
        {
            _spriteRenderer.flipX = !needDefaultFlip;
        }
        _lastTransformPositionX = transform.position.x;
    }
}
