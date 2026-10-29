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
    private Hideable _hideable;

    [Header("Cursor Settings")]
    public float joystickSpeed = 10f;

    private const float StickDeadZone = 0.1f;

    #endregion

    private void Awake()
    {
        _hideable = Hideable.On(this);
        _hideable.VisibilityChanged += OnVisibilityChanged;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Subscribed for the whole spawn, not in OnEnable: a hidden cursor must still hear the
        // server showing it again.
        IsActive.OnValueChanged += HandleIsActiveChanged;

        if (IsOwner)
            CursorColor.OnValueChanged += HandleColorChanged;
    }

    public override void OnDestroy()
    {
        if (_hideable != null)
            _hideable.VisibilityChanged -= OnVisibilityChanged;

        base.OnDestroy();
    }

    // Every peer: hidden while the player has a character, and during phase 2.
    public bool IsVisible => _hideable.IsVisible;

    public void SetVisible(bool visible)
    {
        _hideable.SetVisible(visible);
    }

    private void OnVisibilityChanged(bool visible)
    {
        if (visible && IsOwner)
        {
            UnityEngine.Cursor.lockState = CursorLockMode.Confined;
            UnityEngine.Cursor.visible = false;
        }

        RefreshClickSubscription();
    }

    private void HandleIsActiveChanged(bool oldValue, bool newValue)
    {
        SetVisible(newValue);
        RefreshClickSubscription();
    }

    private void HandleColorChanged(Color oldValue, Color newValue)
    {
        ChangeColor(newValue);
    }

    // At most one subscription, only while the cursor is active.
    private void RefreshClickSubscription()
    {
        if (_Input == null)
            return;

        _Input.MouseAction1Event -= OnCursorAction;
        if (IsActive.Value)
            _Input.MouseAction1Event += OnCursorAction;
    }

    public override void OnNetworkDespawn()
    {
        // The InputReader is a shared asset that outlives this cursor: without this, a click in
        // the next session still reached the old cursor and its old identity (SelectCharacter).
        if (_Input != null)
        {
            _Input.MouseAction1Event -= OnCursorAction;
            _Input = null;
        }

        IsActive.OnValueChanged -= HandleIsActiveChanged;
        CursorColor.OnValueChanged -= HandleColorChanged;

        OnClick = null;
        base.OnNetworkDespawn();
    }

    public void Init()
    {
        if (IsOwner)
        {
            if (transform.parent && transform.parent.TryGetComponent(out PlayerIdentity player))
            {
                _Input = player.Input;
                RefreshClickSubscription();
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

        // The gamepad stick moves the cursor; otherwise it follows the mouse.
        Vector2 stick = Gamepad.current != null ? Gamepad.current.leftStick.ReadValue() : Vector2.zero;

        if (Mathf.Abs(stick.x) > StickDeadZone || Mathf.Abs(stick.y) > StickDeadZone)
        {
            Vector3 joystickDelta = joystickSpeed * Time.deltaTime * new Vector3(stick.x, stick.y, 0);

            targetPosition += joystickDelta;

            // Block the cursor inside the screen
            targetPosition.x = Mathf.Clamp(targetPosition.x, -8.5f, 8.5f);
            targetPosition.y = Mathf.Clamp(targetPosition.y, -4.5f, 4.5f);
            targetPosition.z = 0;

            // Synchronise cursor position with mouse position
            Vector3 screenPosition = Camera.main.WorldToScreenPoint(targetPosition);

            Mouse.current?.WarpCursorPosition(new Vector2(screenPosition.x, screenPosition.y));
        }
        else if (Mouse.current != null)
        {
            Vector3 mousePosition = Mouse.current.position.ReadValue();

            mousePosition.z = Camera.main.nearClipPlane;
            targetPosition = Camera.main.ScreenToWorldPoint(mousePosition);
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