using System;
using Unity.Netcode;
using UnityEngine;

public abstract class ATrap : NetworkBehaviour
{
    #region [SerializeField] Protected Attributs
    [SerializeField] protected TrapState _state;
    [SerializeField] protected BoxCollider2D _boxColliderOverMouse;
    [SerializeField] protected AudioClip _destroyAudioClip;
    #endregion

    #region Protected Attributs
    protected BoxCollider2D _boxCollider;
    protected TrapState _previousState;
    protected Animator _animator;
    #endregion

    #region Private Attributs
    Vector2 _mousePosition;

    #endregion

    #region Public Attributs
    public Vector3Int cellPosition = new();
    public ulong creatorId; // player that called the server to instantiate the trap
    public int[,] matrix;
    public bool isBombTrap = false;
    #endregion

    public NetworkVariable<bool> needToBeSelected = new NetworkVariable<bool>();
    public NetworkVariable<bool> isSetable = new NetworkVariable<bool>();
    public NetworkVariable<bool> isPlaced = new NetworkVariable<bool>();
    public NetworkVariable<int> trapId = new NetworkVariable<int>();

    // Server only: true once the preview has been shown on a valid cell (cellPosition is meaningful).
    [NonSerialized] public bool hasPreviewCell;

    [Flags]
    public enum TrapState
    {
        None = 0,
        Animate = 1 << 0,    // 1
        Collide = 1 << 1     // 2
    }

    protected virtual void Awake()
    {
        _boxCollider = GetComponent<BoxCollider2D>();
        _animator = GetComponent<Animator>();
        _state |= TrapState.None;
        _previousState = _state;

        _animator.enabled = true;

        
    }

    protected virtual void Start()
    {
        // Set network variables
        if (IsHost)
        {
            isPlaced.Value = false;
            needToBeSelected.Value = true;
        }
    }

    public override void OnNetworkDespawn()
    {
        // Played locally on every peer: an RPC can no longer be sent once despawned
        // (the old OnDestroy did that and threw on every peer).
        if (_destroyAudioClip != null)
            AudioSource.PlayClipAtPoint(_destroyAudioClip, transform.position);

        DestroyTrap();
        base.OnNetworkDespawn();
    }

    protected virtual void DestroyTrap()
    {
        // Custom destroy method if needed
    }

    protected virtual void Update()
    {
        // Check if there is a collision with the mouse

        _mousePosition = TrapUtils.GetMousePosition();

        if (_boxColliderOverMouse == null)
        {
            UpdateComponentOnState();
            return;
        }

        if (needToBeSelected.Value
            && _boxColliderOverMouse.OverlapPoint(_mousePosition)
            && Input.GetMouseButtonDown(0))
        {
            PlayerIdentity localPlayer = PlayerIdentity.LocalPlayer;
            if (localPlayer == null || localPlayer.Cursor == null)
                return;

            ulong cursorId = localPlayer.Cursor.GetComponent<NetworkObject>().NetworkObjectId;
            TrapPlacementManager.Instance.OnPlayerSelectedATrapServerRpc(trapId.Value, localPlayer.OwnerClientId, cursorId);
        }
    }

    virtual protected void FixedUpdate()
    {
        
    }

    private void UpdateComponentOnState()
    {
        if (_previousState != _state)
        {
            _animator.enabled = (_state & TrapState.Animate) == TrapState.Animate;
            _boxCollider.enabled = (_state & TrapState.Collide) == TrapState.Collide;
            _previousState = _state;
        }
    }

    public void RotateShape(int direction)
    {
        int dimensionY = matrix.GetLength(0);
        int dimensionX = matrix.GetLength(1);
        int[,] newMatrix;

        // Normalize the direction to keep it between 0 and 3 rotations
        int rotationStep = direction % 4;  // Invert direction for counterclockwise

        if (rotationStep < 0)
        {
            rotationStep += 4;
        }

        // Rotate the transform of the GameObject
        transform.Rotate(new Vector3(0, 0, 90 * rotationStep));  // Negative to rotate counterclockwise

        switch (rotationStep)
        {
            case 1: // 90 degrees counterclockwise
                newMatrix = new int[dimensionX, dimensionY];
                for (int y = 0; y < dimensionY; y++)
                {
                    for (int x = 0; x < dimensionX; x++)
                    {
                        newMatrix[dimensionX - x - 1, y] = matrix[y, x];
                    }
                }
                break;
            case 2: // 180 degrees (same as 180 degrees clockwise)
                newMatrix = new int[dimensionY, dimensionX];
                for (int y = 0; y < dimensionY; y++)
                {
                    for (int x = 0; x < dimensionX; x++)
                    {
                        newMatrix[dimensionY - y - 1, dimensionX - x - 1] = matrix[y, x];
                    }
                }
                break;
            case 3: // 270 degrees counterclockwise
                newMatrix = new int[dimensionX, dimensionY];
                for (int y = 0; y < dimensionY; y++)
                {
                    for (int x = 0; x < dimensionX; x++)
                    {
                        newMatrix[x, dimensionY - y - 1] = matrix[y, x];
                    }
                }
                break;
            default: // 0 or 360 degrees (no rotation)
                newMatrix = (int[,])matrix.Clone();
                break;
        }
        matrix = newMatrix;
    }


    [Rpc(SendTo.ClientsAndHost)]
    public void ActivateCollideStateClientRpc()
    {
        _state |= TrapState.Collide;
        _state |= TrapState.Animate;
        return;
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void UnActivateCollideStateClientRpc()
    {
        _state &= ~TrapState.Collide;
        _state &= ~TrapState.Animate;
        return;
    }
}
