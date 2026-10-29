using Unity.Netcode;
using UnityEngine;

public class Seashell : ATrap
{
    #region Public Attributes
    public NetworkVariable<bool> CanTeleport = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> IsLinked = new NetworkVariable<bool>(false);
    public NetworkVariable<NetworkObjectReference> LinkedSeashell = new NetworkVariable<NetworkObjectReference>();
    public float ElapsedTime = 0.0f;
    #endregion

    #region [SerializeField] Private Attributes
    [SerializeField] private float _DurationBeforeCanTeleport = 4f;
    #endregion

    override protected void Awake()
    {
        base.Awake();
        matrix = new int[,] {
            { 1 },
        };

        _animator.SetBool("IsOpening", false);
        _animator.SetBool("IsTeleporting", false);
    }

    override protected void Start()
    {
        base.Start();

        if (!IsHost) return;

        OnNewTrapPlaced();
    }

    private void OnEnable()
    {
        if (!IsHost) return;

        _animator.SetBool("IsOpening", false);
        _animator.SetBool("IsTeleporting", false);

        if (TrapPlacementManager.Instance != null)
        {
            TrapPlacementManager.Instance.OnTrapPlaced += OnNewTrapPlaced;
            TrapPlacementManager.Instance.OnTrapRemoved += OnNewTrapPlaced;
        }
    }

    private void OnDisable()
    {
        if (!IsHost) return;

        // Unsubscribe from the event
        if (TrapPlacementManager.Instance != null)
        {
            TrapPlacementManager.Instance.OnTrapPlaced -= OnNewTrapPlaced;
            TrapPlacementManager.Instance.OnTrapRemoved -= OnNewTrapPlaced;
        }
    }

    override protected void FixedUpdate()
    {
        if (!IsHost) return;

        bool isCollideActive = (_state & TrapState.Collide) == TrapState.Collide;

        if (isCollideActive)
        {
            HandleSeashellBehaviour();
        }
    }

    private void HandleSeashellBehaviour()
    {
        if (_animator.GetBool("IsOpening"))
        {
            ElapsedTime = 0.0f;
            return;
        }

        if (ElapsedTime < _DurationBeforeCanTeleport)
        {
            ElapsedTime += Time.deltaTime;
        }
        else
        {
            // Toggle Firing Animation
            _animator.SetBool("IsOpening", true);
            _animator.SetBool("IsTeleporting", false);
            ElapsedTime = 0.0f;
        }
        return;
    }

    public void OnSeashellOpened()
    {
        if (!IsOwner)
            return;

        ChangeStateCanTeleportServerRpc(true);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ChangeStateCanTeleportServerRpc(bool value)
    {
        CanTeleport.Value = value;
        Debug.Log($"CanTeleport updated to {value} on host.");
    }

    public void OnSeashellTeleported()
    {
        _animator.SetBool("IsTeleporting", false);
    }

    public void OnLinkedSeashellTeleportation()
    {
        if (!IsOwner)
            return;

        ChangeStateCanTeleportServerRpc(false);
        _animator.SetBool("IsOpening", false);
        _animator.SetBool("IsTeleporting", true);
        return;
    }


    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void TeleportPlayerServerRpc(ulong playerId)
    {
        // The NetworkVariable itself is never null: the linked shell is checked through its value below.

        // Get the LinkedSeashell GameObject
        GameObject LinkedSeashell = this.LinkedSeashell.Value;

        if (!CanTeleport.Value || !LinkedSeashell || !IsLinked.Value)
        {
            Debug.Log("CanTeleport.Value -> " + CanTeleport.Value);
            Debug.Log("LinkedSeashell -> " + LinkedSeashell);
            Debug.Log("IsLinked.Value -> " + IsLinked.Value);

            Debug.Log("Can't teleport player");
            return;
        }

        // Call a ClientRpc to update the position on all clients
        TeleportPlayerClientRpc(playerId, LinkedSeashell.transform.position);

        ChangeStateCanTeleportServerRpc(false);
        _animator.SetBool("IsOpening", false);
        _animator.SetBool("IsTeleporting", true);
        LinkedSeashell.GetComponent<Seashell>().OnLinkedSeashellTeleportation();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TeleportPlayerClientRpc(ulong playerId, Vector3 position)
    {
        // Players and the replicated character: valid on every peer.
        PlayerCharacter playerObj = Players.TryGet(playerId, out PlayerIdentity player) ? player.Character : null;

        if (playerObj == null)
        {
            Debug.LogError($"Player with ID {playerId} not found.");
            return;
        }

        playerObj.transform.position = position;
        Debug.Log($"Player with ID {playerId} has been teleported to {position}");
    }

    public void OnNewTrapPlaced()
    {
        if (IsLinked.Value)
        {
            return;
        }

        // Get all GameObjects in the scene named seaShell in the scene
        GameObject[] Seashells = GameObject.FindGameObjectsWithTag("Seashell");

        foreach (var seashell in Seashells)
        {
            Seashell tempSeaShell = seashell.GetComponent<Seashell>();

            // Never link a shell to itself or to a shell already linked (IsLinked is the replicated signal).
            if (tempSeaShell == this || tempSeaShell.IsLinked.Value)
            {
                continue;
            }

            if (AttachSeashell(tempSeaShell))
            {
                return;
            }
            ;
        }
    }

    private bool AttachSeashell(Seashell seashell)
    {
        // Trap may be destroy at the same time
        try
        {
            seashell.IsLinked.Value = true;
            seashell.LinkedSeashell.Value = this.gameObject;

            IsLinked.Value = true;
            LinkedSeashell.Value = seashell.gameObject;

            Debug.Log("Linking Seashell with another Seashell");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.Log(e);
            return false;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ResetTrapServerRpc()
    {
        // IsLinked is the guard: the NetworkVariable itself is never null.
        if (IsLinked.Value)
        {
            GameObject gameObject = LinkedSeashell.Value;

            if (gameObject == null)
                return;

            Seashell linkedShell = gameObject.GetComponent<Seashell>();
            linkedShell.IsLinked.Value = false;
            // Clear the value, never the NetworkVariable itself (any later .Value would throw).
            linkedShell.LinkedSeashell.Value = default;
            linkedShell.CanTeleport.Value = false;
            linkedShell.ElapsedTime = 0.0f;

            Debug.Log("Unlinking Seashell with another Seashell");

            linkedShell.OnNewTrapPlaced();
        }
    }

    protected override void DestroyTrap()
    {
        if (!IsHost) return;

        ResetTrapServerRpc();
    }
}