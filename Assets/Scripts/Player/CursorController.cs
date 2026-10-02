using UnityEngine;
using System;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class CursorController : NetworkBehaviour
{

    #region Attributes

    public Action<CursorController> OnClick;

    public NetworkVariable<Color> CursorColor = new(Color.white, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public NetworkVariable<bool> IsActive = new(true, NetworkVariableReadPermission.Everyone);

    private InputReader _Input;

    [Header("Cursor Settings")]
    public float joystickSpeed = 10f;

    #endregion

    public void OnEnable()
    {
        IsActive.OnValueChanged += HandleIsActiveChanged;

        if (!IsOwner) return;

        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = false;

        CursorColor.OnValueChanged += (oldValue, newValue) => ChangeColor(newValue);

        if (_Input != null)
        {
            _Input.MouseAction1Event -= OnCursorAction;
            if (IsActive.Value)
                _Input.MouseAction1Event += OnCursorAction;
        }
    }

    private void OnDisable()
    {
        IsActive.OnValueChanged -= HandleIsActiveChanged;
    }

    private void HandleIsActiveChanged(bool oldValue, bool newValue)
    {
        gameObject.SetActive(newValue);

        if (_Input == null)
            return;

        if (newValue)
            _Input.MouseAction1Event += OnCursorAction;
        else
            _Input.MouseAction1Event -= OnCursorAction;
    }

    public void Init()
    {
        if (IsOwner) {
            if (transform.parent && transform.parent.TryGetComponent(out PlayerIdentity player)) {
                _Input = player.Input;

                _Input.MouseAction1Event += OnCursorAction;
            }
        }
    }

    public void Update()
    {
        if (!IsOwner || !IsClient || !IsActive.Value)
        {
            return;
        }
        HandleMovement();
    }

    private void HandleMovement()
    {
        Vector3 targetPosition = transform.position;

        // If the player is using a joystick
        float joystickX = Input.GetAxis("Horizontal");
        float joystickY = Input.GetAxis("Vertical");

        if (Mathf.Abs(joystickX) > 0.1f || Mathf.Abs(joystickY) > 0.1f)
        {
            // Move the cursor with the joystick
            Vector3 joystickDelta = joystickSpeed * Time.deltaTime * new Vector3(joystickX, joystickY, 0);

            targetPosition += joystickDelta;

            // Block the cursor inside the screen
            targetPosition.x = Mathf.Clamp(targetPosition.x, -8.5f, 8.5f);
            targetPosition.y = Mathf.Clamp(targetPosition.y, -4.5f, 4.5f);
            targetPosition.z = 0;

            // Synchronise cursor position with mouse position
            Vector3 screenPosition = Camera.main.WorldToScreenPoint(targetPosition);

            Mouse.current?.WarpCursorPosition(new Vector2(screenPosition.x, screenPosition.y));
        } else {
            Vector3 mousePosition = Input.mousePosition;

            mousePosition.z  = Camera.main.nearClipPlane;            
            targetPosition   = Camera.main.ScreenToWorldPoint(mousePosition);
            targetPosition.z = 0;
        }

        transform.position = targetPosition;
    }


    private void OnCursorAction()
    {
        if (IsOwner)
            OnClick?.Invoke(this);
    }

    public void SetColor(Color color)
    {
        CursorColor.Value = color;
    }

    public void ChangeColor(Color color)
    {
        if (TryGetComponent<SpriteRenderer>(out var sprite))
            sprite.color = color;
    }

    [SerializeField]
    private GameObject _HoveredEntity;

    public void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
            _HoveredEntity = collision.gameObject;
    }

    public void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player") && _HoveredEntity == collision.gameObject)
            _HoveredEntity = null;
    }

    public GameObject HoveredEntity()
    {
        return _HoveredEntity;
    }
}
