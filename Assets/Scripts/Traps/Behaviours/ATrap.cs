using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

public abstract class ATrap : NetworkBehaviour
{
    [Flags]
    public enum TrapState
    {
        None = 0,
        Animate = 1 << 0,
        Collide = 1 << 1
    }

    [SerializeField] protected TrapState _state;
    [SerializeField] protected BoxCollider2D _boxColliderOverMouse;
    [SerializeField] protected AudioClip _destroyAudioClip;

    // Former per-trap fields: the attributes keep the clips already assigned in the prefabs.
    [Tooltip("Played on every peer by PlayActionSound(): hit, shot, fire...")]
    [FormerlySerializedAs("_AudioClip"), FormerlySerializedAs("_audioClip")]
    [SerializeField] protected AudioClip _actionSound;

    protected BoxCollider2D _boxCollider;
    protected TrapState _previousState;
    protected Animator _animator;

    private Vector2 _mousePosition;

    public Vector3Int cellPosition = new();
    // Server only: true once the preview has been shown on a valid cell (cellPosition is meaningful).
    [NonSerialized] public bool hasPreviewCell;
    // Player who chose the trap (server only).
    public ulong creatorId;
    public int[,] matrix;
    public bool isBombTrap = false;

    public NetworkVariable<bool> needToBeSelected = new NetworkVariable<bool>();
    public NetworkVariable<bool> isSetable = new NetworkVariable<bool>();
    public NetworkVariable<bool> isPlaced = new NetworkVariable<bool>();
    public NetworkVariable<int> trapId = new NetworkVariable<int>();

    // True while the trap is live (phase 2): behaviours only run then.
    protected bool IsActive => (_state & TrapState.Collide) == TrapState.Collide;

    // Set on every peer just before the end-of-game cleanup, so the traps do not all play their destroy sound at once.
    public static bool MuteDestroySound { get; set; }

    protected virtual void Awake()
    {
        _boxCollider = GetComponent<BoxCollider2D>();
        _animator = GetComponent<Animator>();
        _previousState = _state;
        _animator.enabled = true;
    }

    protected virtual void Start()
    {
        if (IsServer)
        {
            isPlaced.Value = false;
            needToBeSelected.Value = true;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // A new trap means a new game (or a new placement phase): destroy sounds are allowed again.
        MuteDestroySound = false;
    }

    public override void OnNetworkDespawn()
    {
        // Played locally on every peer: an RPC can no longer be sent once despawned.
        // Skipped during the end-of-game cleanup (TrapPlacementManager.DespawnAllTraps).
        if (_destroyAudioClip != null && !MuteDestroySound)
            AudioSource.PlayClipAtPoint(_destroyAudioClip, transform.position);

        DestroyTrap();
        base.OnNetworkDespawn();
    }

    protected virtual void DestroyTrap()
    {
    }

    protected virtual void Update()
    {
        // Applied to every trap: no animation and no collision outside phase 2.
        ApplyStateToComponents();

        if (_boxColliderOverMouse == null || !needToBeSelected.Value)
            return;

        _mousePosition = TrapUtils.GetMousePosition();

        if (_boxColliderOverMouse.OverlapPoint(_mousePosition) && Input.GetMouseButtonDown(0))
        {
            PlayerIdentity localPlayer = PlayerIdentity.LocalPlayer;
            if (localPlayer == null || localPlayer.Cursor == null)
                return;

            ulong cursorId = localPlayer.Cursor.GetComponent<NetworkObject>().NetworkObjectId;
            TrapPlacementManager.Instance.OnPlayerSelectedATrapServerRpc(trapId.Value, localPlayer.OwnerClientId, cursorId);
        }
    }

    protected virtual void FixedUpdate()
    {
    }

    private void ApplyStateToComponents()
    {
        if (_previousState == _state)
            return;

        _animator.enabled = (_state & TrapState.Animate) == TrapState.Animate;
        _boxCollider.enabled = (_state & TrapState.Collide) == TrapState.Collide;
        _previousState = _state;
    }

    // Server only: plays _actionSound at the trap position on every peer.
    protected void PlayActionSound()
    {
        if (IsServer && _actionSound != null)
            PlayActionSoundClientRpc(transform.position);
    }

    // Server only: a player was hit by this trap. The hit is detected on the player's own
    // machine (PlayerCharacter.TrapPunch), where his physics runs, then reported here.
    // Overridden by the traps that play a sound on hit (Spikes, Shuriken).
    public virtual void OnPlayerHit()
    {
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlayActionSoundClientRpc(Vector3 position)
    {
        if (_actionSound != null)
            AudioSource.PlayClipAtPoint(_actionSound, position);
    }

    public void RotateShape(int direction)
    {
        int dimensionY = matrix.GetLength(0);
        int dimensionX = matrix.GetLength(1);
        int[,] newMatrix;

        // Keep the step between 0 and 3 quarter turns (counterclockwise).
        int rotationStep = direction % 4;
        if (rotationStep < 0)
            rotationStep += 4;

        transform.Rotate(new Vector3(0, 0, 90 * rotationStep));

        switch (rotationStep)
        {
            case 1: // 90 degrees counterclockwise
                newMatrix = new int[dimensionX, dimensionY];
                for (int y = 0; y < dimensionY; y++)
                    for (int x = 0; x < dimensionX; x++)
                        newMatrix[dimensionX - x - 1, y] = matrix[y, x];
                break;
            case 2: // 180 degrees
                newMatrix = new int[dimensionY, dimensionX];
                for (int y = 0; y < dimensionY; y++)
                    for (int x = 0; x < dimensionX; x++)
                        newMatrix[dimensionY - y - 1, dimensionX - x - 1] = matrix[y, x];
                break;
            case 3: // 270 degrees counterclockwise
                newMatrix = new int[dimensionX, dimensionY];
                for (int y = 0; y < dimensionY; y++)
                    for (int x = 0; x < dimensionX; x++)
                        newMatrix[x, dimensionY - y - 1] = matrix[y, x];
                break;
            default:
                newMatrix = (int[,])matrix.Clone();
                break;
        }

        matrix = newMatrix;
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void ActivateCollideStateClientRpc()
    {
        _state |= TrapState.Collide | TrapState.Animate;
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    public void UnActivateCollideStateClientRpc()
    {
        _state &= ~(TrapState.Collide | TrapState.Animate);
    }
}