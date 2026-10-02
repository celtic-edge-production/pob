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

    public override void OnDestroy()
    {
        // Play the destroy sound
     
        if (_destroyAudioClip != null)
        {
            PlaySoundServerRpc();
            //SoundManagerSO.PlaySoundFXClip(_destroyAudioClip, transform.position, 1.0f);
        }

        DestroyTrap();

        base.OnDestroy();
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

        if (needToBeSelected.Value)
        {
            // Detect if the mouse is over the trap and if the player clicked on it
            if (_boxColliderOverMouse.OverlapPoint(_mousePosition) && Input.GetMouseButtonDown(0))
            {
                Debug.Log("Trap selected by a player");

                // Bug corrige : "ConnectedClients" n'est accessible que sur le serveur.
                // Cette methode Update() tourne sur TOUS les pairs sans aucune garde
                // IsHost/IsServer - la selection de piege au clic plantait donc
                // systematiquement pour tout client non-hote. GetLocalPlayerObject()
                // est l'equivalent officiellement documente, valide cote client.
                var playerObject = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();

                if (playerObject == null)
                    return;

                ulong playerId = playerObject.OwnerClientId;

                // Get Cursor GameObject in child of player prefab
                GameObject cursorGameObject = playerObject.transform.Find("Cursor(Clone)").gameObject;

                if (cursorGameObject != null)
                {
                    Debug.Log("Cursor found, OnPlayerSelectedATrapServerRpc calling...");
                    ulong networkId = cursorGameObject.GetComponent<NetworkObject>().NetworkObjectId;
                    TrapPlacementManager.Instance.OnPlayerSelectedATrapServerRpc(trapId.Value, playerId, networkId);
                }
            }
        }
        //else
        //{
        //    // Detect if the mouse is over the BoxCollider2D
        //    if (_boxColliderOverMouse.OverlapPoint(_mousePosition))
        //    {
        //        _state |= TrapState.Animate;
        //    }
        //    else
        //    {
        //        _state &= ~TrapState.Animate;
        //    }
        //}
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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PlaySoundServerRpc()
    {
        PlaySoundClientRpc(transform.position);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlaySoundClientRpc(Vector3 position)
    {
        AudioSource.PlayClipAtPoint(_destroyAudioClip, position);
    }
}
