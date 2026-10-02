using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class Seashell : ATrap
{
    #region Public Attributs
    //public GameObject JumeledSeashell;
    public NetworkVariable<bool> CanTeleport = new NetworkVariable<bool>(false);
    public NetworkVariable<bool> IsLinked = new NetworkVariable<bool>(false);
    public NetworkVariable<NetworkObjectReference> JumeledSeashell = new NetworkVariable<NetworkObjectReference>();
    public float ElapsedTime = 0.0f;
    #endregion

    #region [SerializeField] Private Attributs
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

    public void OnJumeledSeashellTeleportation()
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
        // Bug corrige : "this.JumeledSeashell == null" testait le wrapper
        // NetworkVariable<T> lui-meme, qui n'est jamais null une fois construit -
        // ce garde-fou ne se declenchait jamais. Le vrai test utile (le GameObject
        // resolu depuis .Value) est deja fait plus bas via "!JumeledSeashell".

        // Get the JumeledSeashell GameObject
        GameObject JumeledSeashell = this.JumeledSeashell.Value;

        if (!CanTeleport.Value || !JumeledSeashell || !IsLinked.Value)
        {
            Debug.Log("CanTeleport.Value -> " + CanTeleport.Value);
            Debug.Log("JumeledSeashell -> " + JumeledSeashell);
            Debug.Log("IsLinked.Value -> " + IsLinked.Value);

            Debug.Log("Can't teleport player");
            return;
        }

        // Call a ClientRpc to update the position on all clients
        TeleportPlayerClientRpc(playerId, JumeledSeashell.transform.position);

        ChangeStateCanTeleportServerRpc(false);
        _animator.SetBool("IsOpening", false);
        _animator.SetBool("IsTeleporting", true);
        JumeledSeashell.GetComponent<Seashell>().OnJumeledSeashellTeleportation();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TeleportPlayerClientRpc(ulong playerId, Vector3 position)
    {
        // Bug corrige : "NetworkManager.Singleton.ConnectedClients" n'est accessible
        // que sur le serveur (meme souci que dans GameRoundManager). Ce ClientRpc
        // s'execute sur tous les clients, donc ca plantait pour tout non-hote.
        // Remplace par une recherche via OwnerClientId, valide sur n'importe quel pair.
        PlayerIdentity playerController = FindObjectsByType<PlayerIdentity>(FindObjectsSortMode.None)
            .FirstOrDefault(p => p.OwnerClientId == playerId);

        if (playerController == null)
        {
            Debug.LogError($"Player with ID {playerId} not found.");
            return;
        }

        GameObject playerObj = playerController.Characters;

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

            // Check to avoid self linking
            // Bug corrige : "tempSeaShell.JumeledSeashell != null" testait le wrapper
            // NetworkVariable<T> lui-meme (toujours non-null), pas sa valeur - ce test
            // ne filtrait jamais rien. IsLinked.Value seul est deja le bon signal reseau.
            if (tempSeaShell == this || tempSeaShell.IsLinked.Value)
            {
                continue;
            }

            if (AttachSeashell(tempSeaShell))
            {
                return;
            };
        }
    }

    private bool AttachSeashell(Seashell seashell)
    {
        // Trap may be destroy at the same time
        try
        {
            seashell.IsLinked.Value = true;
            seashell.JumeledSeashell.Value = this.gameObject;

            IsLinked.Value = true;
            JumeledSeashell.Value = seashell.gameObject;
     
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
        // Bug corrige : "JumeledSeashell != null" testait le wrapper (toujours non-null),
        // IsLinked.Value seul suffit comme garde.
        if (IsLinked.Value)
        {
            GameObject gameObject = JumeledSeashell.Value;

            if (gameObject == null)
                return;

            Seashell linkedShell = gameObject.GetComponent<Seashell>();
            linkedShell.IsLinked.Value = false;
            // Bug corrige : "linkedShell.JumeledSeashell = null;" remplacait le wrapper
            // NetworkVariable<T> entier par null au lieu de vider sa valeur - toute
            // lecture/ecriture future de .Value sur ce coquillage plantait avec un
            // NullReferenceException. Il faut vider la VALEUR, pas le wrapper.
            linkedShell.JumeledSeashell.Value = default;
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
