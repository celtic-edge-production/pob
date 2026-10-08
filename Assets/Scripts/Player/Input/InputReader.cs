using System;
using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(menuName = "Input/Input Reader", fileName = "Input Reader")]
public class InputReader : ScriptableObject
{

    [SerializeField]
    private InputActionAsset _Asset;

    // ACTIONS

    // -- Cursor Actions --
    private InputAction _MouseAction1;
    private InputAction _MouseAction2;

    // -- Movement Actions --
    private InputAction _MoveAction;
    private InputAction _JumpAction;

    // -- Combat Actions --
    private InputAction _AttackAction;
    private InputAction _ThrowAction;

    // -- Editor Actions --
    private InputAction _RotateLeft;
    private InputAction _RotateRight;
    private InputAction _Mirror;

    // -- Menu Actions --
    private InputAction _MenuAction;

    // EVENTS

    public event Action MouseAction1Event;
    public event Action MouseAction2Event;

    public event Action<Vector2> MoveEvent;
    public event Action JumpStartEvent;
    public event Action JumpEvent;

    public event Action AttackEvent;
    public event Action ThrowEventStart;
    public event Action ThrowEventEnd;

    public event Action RotateLeftEvent;
    public event Action RotateRightEvent;
    public event Action MirrorEvent;

    public event Action MenuEvent;

    private void OnEnable()
    {
        _MouseAction1 = _Asset.FindAction("Action 1");
        _MouseAction2 = _Asset.FindAction("Action 2");

        _MoveAction = _Asset.FindAction("Move");
        _JumpAction = _Asset.FindAction("Jump");

        _AttackAction = _Asset.FindAction("Attack");
        _ThrowAction = _Asset.FindAction("Throw");

        _RotateLeft = _Asset.FindAction("Rotate Left");
        _RotateRight = _Asset.FindAction("Rotate Right");
        // Optional: add a "Mirror" button action to the Input Actions asset to enable it.
        _Mirror = _Asset.FindAction("Mirror");

        _MenuAction = _Asset.FindAction("Menu");

        // Listen to the events.
        // Buttons only listen to "performed": a press also raises "started" and a release "canceled",
        // and listening to all three ran each click, punch and rotation two or three times.
        if (_MouseAction1 != null)
        {
            _MouseAction1.performed += OnMouseAction1Event;
        }
        if (_MouseAction2 != null)
        {
            _MouseAction2.performed += OnMouseAction2Event;
        }
        if (_MoveAction != null)
        {
            _MoveAction.started += OnMovePerformed;
            _MoveAction.performed += OnMovePerformed;
            _MoveAction.canceled += OnMovePerformed;
        }
        if (_JumpAction != null)
        {
            _JumpAction.started += OnJumpStarted;
            _JumpAction.performed += OnJumpPerformed;
            _JumpAction.canceled += OnJumpPerformed;
        }
        if (_AttackAction != null)
        {
            _AttackAction.performed += OnAttackPerformed;
        }
        if (_ThrowAction != null)
        {
            _ThrowAction.started += OnThrowStarted;
            _ThrowAction.canceled += OnThrowEnded;
        }
        if (_RotateLeft != null)
        {
            _RotateLeft.performed += OnRotateLeftPerformed;
        }
        if (_RotateRight != null)
        {
            _RotateRight.performed += OnRotateRightPerformed;
        }
        if (_Mirror != null)
        {
            _Mirror.performed += OnMirrorPerformed;
        }
        if (_MenuAction != null)
        {
            _MenuAction.performed += OnMenuPerformed;
        }

        _Asset.Enable();
    }

    private void OnDisable()
    {
        // Stop listening to the events
        if (_Asset != null)
        {
            if (_MouseAction1 != null)
            {
                _MouseAction1.performed -= OnMouseAction1Event;
            }
            if (_MouseAction2 != null)
            {
                _MouseAction2.performed -= OnMouseAction2Event;
            }
            if (_MoveAction != null)
            {
                _MoveAction.started -= OnMovePerformed;
                _MoveAction.performed -= OnMovePerformed;
                _MoveAction.canceled -= OnMovePerformed;
            }
            if (_JumpAction != null)
            {
                _JumpAction.started -= OnJumpStarted;
                _JumpAction.performed -= OnJumpPerformed;
                _JumpAction.canceled -= OnJumpPerformed;
            }
            if (_AttackAction != null)
            {
                _AttackAction.performed -= OnAttackPerformed;
            }
            if (_ThrowAction != null)
            {
                _ThrowAction.started -= OnThrowStarted;
                _ThrowAction.canceled -= OnThrowEnded;
            }
            if (_RotateLeft != null)
            {
                _RotateLeft.performed -= OnRotateLeftPerformed;
            }
            if (_RotateRight != null)
            {
                _RotateRight.performed -= OnRotateRightPerformed;
            }
            if (_Mirror != null)
            {
                _Mirror.performed -= OnMirrorPerformed;
            }
            if (_MenuAction != null)
            {
                _MenuAction.performed -= OnMenuPerformed;
            }
        }

        _Asset.Disable();
    }

    // Event Handlers

    private void OnMouseAction1Event(InputAction.CallbackContext context)
    {
        MouseAction1Event?.Invoke();
    }

    private void OnMouseAction2Event(InputAction.CallbackContext context)
    {
        MouseAction2Event?.Invoke();
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        MoveEvent?.Invoke(context.ReadValue<Vector2>());
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        JumpEvent?.Invoke();
    }

    private void OnJumpStarted(InputAction.CallbackContext context)
    {
        JumpStartEvent?.Invoke();
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        AttackEvent?.Invoke();
    }

    private void OnThrowStarted(InputAction.CallbackContext context)
    {
        ThrowEventStart?.Invoke();
    }

    private void OnThrowEnded(InputAction.CallbackContext context)
    {
        ThrowEventEnd?.Invoke();
    }

    private void OnRotateLeftPerformed(InputAction.CallbackContext context)
    {
        RotateLeftEvent?.Invoke();
    }

    private void OnRotateRightPerformed(InputAction.CallbackContext context)
    {
        RotateRightEvent?.Invoke();
    }

    private void OnMirrorPerformed(InputAction.CallbackContext context)
    {
        MirrorEvent?.Invoke();
    }

    private void OnMenuPerformed(InputAction.CallbackContext context)
    {
        MenuEvent?.Invoke();
    }
}