using Unity.Netcode;
using UnityEngine;

public class TilemapHover : NetworkBehaviour
{
    // TODO: create this class when player spawn and assign it to the player

    public Vector3 _worldPosition;
    private Vector3 _screenPosition;

    private ulong _playerId;

    public NetworkVariable<bool> isSettingTrap = new NetworkVariable<bool>();
    public NetworkVariable<bool> canPlaceTrap = new NetworkVariable<bool>();

    private InputReader _Input;

    public void OnEnable()
    {
        if (!IsOwner) return;

        _playerId = NetworkManager.Singleton.LocalClientId;
    }

    public void OnDisable()
    {
        if (!IsOwner) return;

        ResetTilemapHoverServerRpc();
    }

    public void Init()
    {
        if (IsOwner) {
            if (transform.parent && transform.parent.TryGetComponent(out PlayerIdentity player)) {
                _Input = player.Input;

                _Input.RotateLeftEvent   += OnRotateLeft;
                _Input.RotateRightEvent  += OnRotateRight;
                _Input.MouseAction1Event += OnMouseAction1;
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void ResetTilemapHoverServerRpc()
    {
        // Bug corrige : l'ancien code verifiait "if (!IsOwner) return;" a l'interieur
        // meme du ServerRpc. Cote serveur, IsOwner demande "le serveur est-il proprietaire
        // de cet objet ?", pas "l'appelant l'etait-il ?" — ce qui bloquait le reset pour
        // n'importe quel client normal (ca ne marchait que pour l'hote lui-meme).
        // RequireOwnership = false a ete retire : NGO refuse deja nativement un appel
        // venant d'un non-proprietaire, donc la verification manuelle etait a la fois
        // fausse et redondante.
        isSettingTrap.Value = false;
        canPlaceTrap.Value = false;
    }

    void Update()
    {
        if (!IsOwner || TrapPlacementManager.Instance == null) return;

        GetMousePosition();

        _playerId = NetworkManager.Singleton.LocalClientId;

        TrapPlacementManager.Instance.CheckMouseOnTileServerRpc(_worldPosition, _playerId);
    }

    private void OnMouseAction1()
    {
        if (canPlaceTrap.Value && isSettingTrap.Value) {
            ulong networkId = GetComponent<NetworkObject>().NetworkObjectId;

            TrapPlacementManager.Instance.OnPlayerPlaceTrapServerRpc(_worldPosition, _playerId, networkId);
        }
    }

    private void OnRotateLeft()
    {
        if (canPlaceTrap.Value)
            TrapPlacementManager.Instance.OnPlayerRotateTrapServerRpc(1, _playerId);
    }

    private void OnRotateRight()
    {
        if (canPlaceTrap.Value)
            TrapPlacementManager.Instance.OnPlayerRotateTrapServerRpc(-1, _playerId);
    }

    private void GetMousePosition()
    {
        _worldPosition = TrapUtils.GetMousePosition();
        transform.position = _worldPosition;
    }
}
