using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TrapPlacementManager : NetworkSingleton<TrapPlacementManager>
{
    [System.Serializable]
    public struct TrapData : INetworkSerializable, IEquatable<TrapData>
    {
        public int trapId;
        public ulong networkObjectId;

        public TrapData(int trapId, ulong networkObjectId)
        {
            this.trapId = trapId;
            this.networkObjectId = networkObjectId;
        }

        // Serialization for sending data over the network
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref trapId);
            serializer.SerializeValue(ref networkObjectId);
        }

        // Implement IEquatable<TrapData>
        public bool Equals(TrapData other)
        {
            return trapId == other.trapId && networkObjectId == other.networkObjectId;
        }

        // Override GetHashCode for consistency with Equals
        public override int GetHashCode()
        {
            return HashCode.Combine(trapId, networkObjectId);
        }

        // Optional: Override Equals(Object) for general equality checks
        public override bool Equals(object obj)
        {
            return obj is TrapData other && Equals(other);
        }
    }

    [SerializeField] private Sprite _sprite;
    [SerializeField] private bool debug = false;
    [SerializeField] PhaseManager _PhaseManager;
    [SerializeField] private float _OffsetSpawn = 100.0f;

    private Tilemap _trapTileMap;
    private Tilemap _paternTileMap;
    private ATrap[] _gameObjectArray;

    NetworkVariable<int> _lastId = new NetworkVariable<int>();
    NetworkList<TrapData> _placedTrap = new();
    NetworkList<TrapData> _notPlacedTrap = new();
    NetworkList<TrapData> _notChoosedTrap = new();

    private NetworkVariable<bool> _CanChangePhase = new();

    // Event for callbacks
    public event System.Action OnTrapPlaced;
    public event System.Action OnTrapRemoved;

    protected override void OnSingletonAwake()
    {
        _trapTileMap = GameObject.Find("Trap").GetComponent<Tilemap>();
        _paternTileMap = GameObject.Find("Background").GetComponent<Tilemap>();
        LoadAllPrefab();
    }

    private void OnEnable()
    {
        if (_notPlacedTrap == null) return;

        _notPlacedTrap.OnListChanged += OnNotPlacedListChangedHandler;
    }

    private void OnDisable()
    {
        if (_notPlacedTrap == null) return;

        _notPlacedTrap.OnListChanged -= OnNotPlacedListChangedHandler;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ChangeStatePhaseServerRpc(bool newState)
    {
        if (!IsOwner) return;

        _CanChangePhase.Value = newState;
    }

    private void OnNotPlacedListChangedHandler(NetworkListEvent<TrapData> changeEvent)
    {
        // Check if the is remove event and if count is 0
        if (changeEvent.Type == NetworkListEvent<TrapData>.EventType.Remove && _notPlacedTrap.Count == 0 && _notChoosedTrap.Count == 0)
        {
            // Change Phase Gameplay to 2
            if (_CanChangePhase.Value && IsServer)
            {
                Debug.Log("OnNotPlacedListChangedHandler - All traps placed, changing phase to 2");
                ChangePhaseGameplayServerRpc();
                ChangeStatePhaseServerRpc(false);
            }
        }
    }

    public void HandlePlayerLeft(ulong clientId)
    {
        if (!IsServer)
            return;

        // Case 1: the player had chosen a trap but not placed it. It would block the phase forever.
        foreach (TrapData trapData in _notPlacedTrap)
        {
            ATrap trap = GetTrapFromNetworkId(trapData.networkObjectId);

            if (trap != null && trap.creatorId == clientId)
            {
                // Removing it may trigger the phase change if it was the last trap to place.
                _notPlacedTrap.Remove(trapData);
                trap.GetComponent<NetworkObject>().Despawn(true);
                return;
            }
        }

        // Case 2: the player had not chosen yet. The chest holds one trap per player, so drop a
        // spare one, then re-run the "everyone has chosen" check he would have triggered.
        if (_notChoosedTrap.Count > 0)
        {
            TrapData spare = _notChoosedTrap[_notChoosedTrap.Count - 1];
            _notChoosedTrap.RemoveAt(_notChoosedTrap.Count - 1);

            ATrap spareTrap = GetTrapFromNetworkId(spare.networkObjectId);
            if (spareTrap != null)
                spareTrap.GetComponent<NetworkObject>().Despawn(true);

            CloseUiToSelectTrapServerRpc();
        }

        // Case 3: his trap was already placed. Nothing to do, it stays on the map.
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ChangePhaseGameplayServerRpc()
    {
        if (!IsServer) return;

        _PhaseManager.SwitchGamePhaseServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void CloseUiToSelectTrapServerRpc()
    {
        Debug.Log("CloseUiToSelectTrap Called - (nbrPlayer: " + NetworkManager.Singleton.ConnectedClients.Count + ", nbr Trap: " + _notPlacedTrap.Count);

        if (NetworkManager.Singleton.ConnectedClients.Count == _notPlacedTrap.Count)
        {
            // find the trap chest
            GameObject trapChest = GameObject.Find("TrapChest(Clone)");

            if (trapChest != null)
            {
                // Get chestUi from child of the trapChest
                GameObject chestUi = trapChest.GetComponent<TrapChest>().Image;
                if (chestUi != null)
                {
                    chestUi.GetComponent<ChestUi>().StartClosingAnimationServerRpc();
                    ShowAllPlacedTrapClientRpc();
                }
            }
        }
    }

    public void ToggleSelectedTrap()
    {
        foreach (TrapData trapData in _notPlacedTrap)
        {
            ATrap tempTrap = GetTrapFromNetworkId(trapData.networkObjectId);

            tempTrap.isSetable.Value = true;
        }
    }

    private bool AreAllNetworkVariablesInitialized()
    {
        // Check if all lists in the network variables are initialized (i.e., not null)
        return _placedTrap != null && _notPlacedTrap != null && _notChoosedTrap != null;
    }

    private void LoadAllPrefab()
    {
        _gameObjectArray = Resources.LoadAll<ATrap>("Prefabs/Traps/Traps");
    }

    private NetworkObject GetNetworkObjectById(ulong networkObjectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var networkObject))
        {
            return networkObject;
        }
        else
        {
            return null;
        }
    }

    private ATrap GetTrapFromNetworkId(ulong networkObjectId)
    {
        NetworkObject networkObject = GetNetworkObjectById(networkObjectId);

        if (networkObject == null)
        {
            return null;
        }

        return networkObject.GetComponent<ATrap>();
    }

    public void RemoveTrap(Vector3 worldPosition)
    {
        if (!IsHost) return;

        // Initial Pose of the trap
        Vector3Int cellPosition = _trapTileMap.WorldToCell(worldPosition);

        // Get the first tile at the position
        TileBase tile = _trapTileMap.GetTile(cellPosition);
        
        if (tile is not TrapTile)
        {
            Debug.Log("No trap found at the position");
            return;
        }

        TrapTile trapTile = (TrapTile)tile; // TODO: check if this cast is safe
        TrapTile tempTrapTile;
        TileBase tempTileBase;

        if (tile == null || tile is not TrapTile)
        {
            return;
        }

        // Id of the trap to delete
        int id = trapTile.id;

        // Remove the trap from the dict of traps
        foreach (TrapData trapData in _placedTrap)
        {
            if (trapData.trapId == id)
            {
                _placedTrap.Remove(trapData);
                break;
            }
        }

        // Loop through the bounds of the tilemap
        foreach (var pos in _trapTileMap.cellBounds.allPositionsWithin)
        {
            // Check if there's a tile at this position
            if (_trapTileMap.HasTile(pos))
            {
                // Check if the tile at this position matches the trap tile
                tempTileBase = _trapTileMap.GetTile(pos);
                if (tempTileBase is TrapTile)
                {
                    tempTrapTile = (TrapTile)tempTileBase;

                    if (tempTrapTile.id == id)
                    {
                        _trapTileMap.SetTile(pos, null);
                    }
                }
            }
        }

        // destroy the trap
        // Meme correctif que Arrow.cs/BombTrap.cs/TrapChest.cs : Despawn() plutot que
        // Destroy() direct sur un objet reseau spawn. RemoveTrap() est deja garde par
        // "if (!IsHost) return;" plus haut, donc on est bien cote serveur ici.
        NetworkObject trapNetworkObject = trapTile.trapPrefab.GetComponent<NetworkObject>();

        if (trapNetworkObject != null)
            trapNetworkObject.Despawn(true);
        else
            Destroy(trapTile.trapPrefab.gameObject);
        OnTrapRemoved?.Invoke();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void OnPlayerPlaceTrapServerRpc(Vector3 worldPosition, ulong playerId, ulong networkId, RpcParams rpcParams = default)
    {
        if (!RpcValidation.SenderMatches(rpcParams, playerId))
            return;

        ATrap trap = GetNotPlacedTrapByPlayerId(playerId);

        if (!trap || !trap.isSetable.Value) return;

        if (_notPlacedTrap == null) return;

        if (PlaceTrap(worldPosition, trap))
        {
            // Remove the trap from the list of not placed traps
            foreach (TrapData trapData in _notPlacedTrap)
            {
                if (trapData.trapId == trap.trapId.Value)
                {
                    _notPlacedTrap.Remove(trapData);

                    NetworkObject networkObject = GetNetworkObjectById(networkId);

                    if (networkObject != null)
                    {
                        networkObject.gameObject.GetComponent<TilemapHover>().isSettingTrap.Value = false;
                    }

                    break;
                }
            }
            OnTrapPlaced?.Invoke();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void OnPlayerSelectedATrapServerRpc(int trapId, ulong playerId, ulong cursordId, RpcParams rpcParams = default)
    {
        if (!RpcValidation.SenderMatches(rpcParams, playerId))
            return;

        ATrap trap = null;

        foreach (var dictTrapElem in _notChoosedTrap)
        {
            ATrap tempTrap = GetTrapFromNetworkId(dictTrapElem.networkObjectId);

            if (trapId == tempTrap.trapId.Value)
            {
                trap = tempTrap;
                Debug.Log("OnPlayerSelectedATrapServerRpc Trap found.");
                break;
            }
        }

        if (trap == null) return;

        // Check if the player is already the owner of a trap
        foreach (var dictTrapElem in _notPlacedTrap)
        {
            ATrap tempTrap = GetTrapFromNetworkId(dictTrapElem.networkObjectId);

            if (tempTrap.creatorId == playerId)
            {
                Debug.Log("OnPlayerSelectedATrapServerRpc already have a trap.");
                return;
            }
        }

        trap.creatorId = playerId;
        trap.needToBeSelected.Value = false;

        // Get Network Id of the trap
        ulong networkId = trap.GetComponent<NetworkObject>().NetworkObjectId;

        // Change the trap dict from _notChoosedTrap to _notPlacedTrap
        _notPlacedTrap.Add(new TrapData(trap.trapId.Value, networkId));

        foreach (TrapData trapData in _notChoosedTrap)
        {
            if (trapId == trapData.trapId)
            {
                _notChoosedTrap.Remove(trapData);
                break;
            }
        }

        // Get the Cursor GameObject
        GameObject playerCursor = GetNetworkObjectById(cursordId).gameObject;
        playerCursor.GetComponent<TilemapHover>().isSettingTrap.Value = true;
        playerCursor.GetComponent<TilemapHover>().canPlaceTrap.Value = true;

        Debug.Log("OnPlayerSelectedATrapServerRpc success by : " + playerId);
        CloseUiToSelectTrapServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void CreateTrapToBeSelectedServerRpc()
    {
        // Generate a random trap
        ATrap trap = Instantiate(
            _gameObjectArray[UnityEngine.Random.Range(0, _gameObjectArray.Length)]
        );

        if (trap == null) return;

        Debug.Log("Name if the trap created by the chest -> " + trap.gameObject.name);

        TrapChest trapChest = GameObject.Find("TrapChest(Clone)").GetComponent<TrapChest>();
        if (trapChest == null) return;
        ChestUi chestUi = trapChest.Image.GetComponent<ChestUi>();
        if (chestUi == null) return;
        RectTransform rectTransform = chestUi.GetComponent<RectTransform>();
        if (rectTransform == null) return;

        // BUG SIGNALE (non corrige) : "Screen.height / Screen.height" vaut toujours 1.0,
        // quel que soit l'ecran - un ratio qui se divise par lui-meme ne sert a rien.
        // C'est presque certainement une erreur de copier-coller (peut-etre
        // "Screen.width / Screen.height" pour un vrai ratio d'aspect ?). Je ne corrige
        // pas a l'aveugle : je ne peux pas verifier visuellement le bon resultat sans
        // Unity, et une mauvaise supposition introduirait un autre bug visuel.
        float screenRatio =  Screen.height / Screen.height;

        float halfWidth = rectTransform.rect.width / 2;
        float halfHeight = rectTransform.rect.height / 2;

        float widthChestUi = halfWidth - _OffsetSpawn < 0 ? halfWidth : halfWidth - _OffsetSpawn;
        float heightChestUi = halfHeight - _OffsetSpawn < 0 ? halfHeight : halfHeight - _OffsetSpawn;

        trap.transform.position = new Vector3((UnityEngine.Random.Range(-widthChestUi, widthChestUi) * screenRatio) / 100.0f, 0,
            (UnityEngine.Random.Range(-heightChestUi, heightChestUi) * screenRatio) / 100.0f);
        Debug.Log("trap.transform.position  -> " + trap.transform.position);


        // Call server network spawn
        trap.GetComponent<NetworkObject>().Spawn();
        trap.creatorId = 0; // 0 is the host (server Id)
        trap.trapId.Value = _lastId.Value;

        // Get Network Id of the trap
        ulong networkId = trap.GetComponent<NetworkObject>().NetworkObjectId;

        // Add the trap to the list of non placed traps
        _notChoosedTrap.Add(new TrapData(trap.trapId.Value, networkId));
        _lastId.Value++;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void OnPlayerRotateTrapServerRpc(int position, ulong playerId, RpcParams rpcParams = default)
    {
        if (!RpcValidation.SenderMatches(rpcParams, playerId))
            return;

        ATrap trap = GetNotPlacedTrapByPlayerId(playerId);

        if (trap)
        {
            if (debug)
            {
                UnDrawLastTiles(trap);
            }
            trap.RotateShape(position);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void OnPlayerRemoveTrapServerRpc(Vector3 worldPosition)
    {
        RemoveTrap(worldPosition);
    }

    private bool IsOtherTrapBlockPlacement(Vector3Int cellPosition, ATrap trap)
    {
        for (int i = 0; i < trap.matrix.GetLength(0); i++)
        {
            for (int j = 0; j < trap.matrix.GetLength(1); j++)
            {
                if (trap.matrix[i, j] != 1)
                {
                    continue;
                }

                Vector3Int tilePosition = new Vector3Int(cellPosition.x + j, cellPosition.y - i, 0);

                // Check if there is no trap placed on the tile
                if (_trapTileMap.HasTile(tilePosition))
                {
                    return false;
                }
            }
        }
        return true;
    }

    public bool PlaceTrap(Vector3 worldPosition, ATrap trap)
    {
        Vector3Int cellPosition = _trapTileMap.WorldToCell(worldPosition);
        TrapTile trapTile = ScriptableObject.CreateInstance<TrapTile>();
        trapTile.trapPrefab = trap;
        trapTile.id = trap.trapId.Value;

        if (debug)
        {
            trapTile.sprite = _sprite;
        }

        // && !IsOtherTrapBlockPlacement(cellPosition, trap)
        if (trap.isBombTrap)
        {
            Debug.Log("Trying to place bomb trap at: " + cellPosition);
            BombTrap bombTrap = (BombTrap)trap;

            bombTrap.ExplodeBomb(cellPosition);
            return true;
        }

        // Check if the trap can be placed
        if (!IsOtherTrapBlockPlacement(cellPosition, trap) /*!_trapSetable*/)
        {
            return false;
        }

        if (trap.isPlaced.Value)
        {
            return false;
        }

        Debug.Log("PlaceTrap at " + cellPosition);

        for (int i = 0; i < trap.matrix.GetLength(0); i++)
        {
            for (int j = 0; j < trap.matrix.GetLength(1); j++)
            {
                if (trap.matrix[i, j] != 1)
                {
                    continue;
                }

                Vector3Int tilePosition = new Vector3Int(cellPosition.x + j, cellPosition.y - i, 0);

                _trapTileMap.SetTile(tilePosition, trapTile);
            }
        }
        if (debug)
        {
            UnDrawLastTiles(trap);
        }

        trap.isPlaced.Value = true;

        // Get Network Id of the trap
        ulong networkId = trap.GetComponent<NetworkObject>().NetworkObjectId;

        // Add the trap to the placed dict
        _placedTrap.Add(new TrapData(trap.trapId.Value, networkId));
        return true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void CheckMouseOnTileServerRpc(Vector3 worldPosition, ulong playerId, RpcParams rpcParams = default)
    {
        if (!RpcValidation.SenderMatches(rpcParams, playerId))
            return;

        Vector3Int cellPosition = _paternTileMap.WorldToCell(worldPosition);

        if (!AreAllNetworkVariablesInitialized())
        {
            return;
        }

        ATrap trap = GetNotPlacedTrapByPlayerId(playerId);

        if (trap == null)
        {
            return;
        }

        if (trap.needToBeSelected.Value)
        {
            return;
        }

        if (_paternTileMap.HasTile(cellPosition))
        {
            if (trap.cellPosition != cellPosition)
            {
                if (debug)
                {
                    UnDrawLastTiles(trap);
                }
                trap.cellPosition = cellPosition;
                DrawShapeOfTrap(trap.cellPosition, trap);

                float offsetX = trap.matrix.GetLength(1);
                float offsetY = trap.matrix.GetLength(0);

                if (offsetX > 1 && offsetY > 1)
                {
                    offsetY = (offsetY % 2 == 0) ? offsetY / 2 - 1.0f : offsetY / 2 - 0.5f;
                    offsetX = (offsetX % 2 == 0) ? offsetX / 2 : offsetX / 2;
                    trap.transform.position = trap.cellPosition + new Vector3(offsetX, -offsetY, 0);
                }
                else if (offsetX > 1)
                {
                    offsetX = (offsetX % 2 == 0) ? offsetX / 2 : offsetX / 2;
                    trap.transform.position = trap.cellPosition + new Vector3(offsetX, 0.5f, 0);
                }
                else if (offsetY > 1)
                {
                    offsetY = (offsetY % 2 == 0) ? offsetY / 2 - 2.0f : offsetY / 2 - 1.0f;
                    trap.transform.position = trap.cellPosition + new Vector3(0.5f, -offsetY, 0);
                }
                else
                {
                    trap.transform.position = trap.cellPosition + new Vector3(0.5f, 0.5f, 0);
                }
            }
        }
    }

    private void UnDrawLastTiles(ATrap trap)
    {
        for (int i = 0; i < trap.matrix.GetLength(0); i++)
        {
            for (int j = 0; j < trap.matrix.GetLength(1); j++)
            {
                if (trap.matrix[i, j] != 1)
                {
                    continue;
                }

                Vector3Int tilePosition = new Vector3Int(trap.cellPosition.x + j, trap.cellPosition.y - i, 0);

                if (_paternTileMap.HasTile(tilePosition))
                {
                    _paternTileMap.SetTileFlags(tilePosition, TileFlags.None);
                    _paternTileMap.SetColor(tilePosition, Color.white);
                }
            }
        }
    }

    private void DrawShapeOfTrap(Vector3Int cellPosition, ATrap trap)
    {
        for (int i = 0; i < trap.matrix.GetLength(0); i++)
        {
            for (int j = 0; j < trap.matrix.GetLength(1); j++)
            {
                if (trap.matrix[i, j] != 1)
                {
                    continue;
                }

                Vector3Int tilePosition = new Vector3Int(cellPosition.x + j, cellPosition.y - i, 0);

                if (_paternTileMap.HasTile(tilePosition))
                {
                    if (debug)
                    {
                        _paternTileMap.SetTileFlags(tilePosition, TileFlags.None);
                        _paternTileMap.SetColor(tilePosition, Color.green);
                    }
                }
            }
        }
    }

    private ATrap GetNotPlacedTrapByPlayerId(ulong playerId)
    {
        foreach (var dictTrapElem in _notPlacedTrap)
        {
            ATrap tempTrap = GetTrapFromNetworkId(dictTrapElem.networkObjectId);

            if (tempTrap.creatorId == playerId)
            {
                return tempTrap;
            }
        }
        return null;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void CreateMultipleRandomTrapServerRpc()
    {
        if (!IsHost) return;

        // Get the number of players
        int numberOfPlayers = NetworkManager.Singleton.ConnectedClients.Count;

        for (int i = 0; i < numberOfPlayers; i++)
        {
            CreateTrapToBeSelectedServerRpc();
        }

        // Able to change phase
        ChangeStatePhaseServerRpc(true);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void DestroyRandomCreatedTrapServerRpc()
    {
        // TODO: implement in case there are too many traps in the selection
        if (!IsHost) return;
        return;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void HideAllPlacedTrapServerRpc()
    {
        HideAllPlacedTrapClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void HideAllPlacedTrapClientRpc()
    {
        // Loop over all placed trap list get them and unactivate them
        foreach (TrapData trapData in _placedTrap)
        {
            ATrap tempTrap = GetTrapFromNetworkId(trapData.networkObjectId);

            tempTrap.gameObject.SetActive(false);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void ShowAllPlacedTrapClientRpc()
    {
        // Loop over all placed trap list get them and activate them
        foreach (TrapData trapData in _placedTrap)
        {
            ATrap tempTrap = GetTrapFromNetworkId(trapData.networkObjectId);

            tempTrap.gameObject.SetActive(true);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ActivateCollideStateTrapServerRpc()
    {
        // Loop over all placed trap list, get them and switch state to collide
        foreach (TrapData trapData in _placedTrap)
        {
            ATrap tempTrap = GetTrapFromNetworkId(trapData.networkObjectId);

            tempTrap.ActivateCollideStateClientRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void UnActivateCollideStateTrapServerRpc()
    {
        foreach (TrapData trapData in _placedTrap)
        {
            ATrap tempTrap = GetTrapFromNetworkId(trapData.networkObjectId);

            tempTrap.UnActivateCollideStateClientRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RemoveTrapServerRpc()
    {
        // Clean all traps from all lists

        foreach (TrapData trapData in _placedTrap)
        {
            ATrap tempTrap = GetTrapFromNetworkId(trapData.networkObjectId);

            tempTrap.gameObject.GetComponent<NetworkObject>().Despawn(true);
        }

        foreach (TrapData trapData in _notPlacedTrap)
        {
            ATrap tempTrap = GetTrapFromNetworkId(trapData.networkObjectId);

            tempTrap.gameObject.GetComponent<NetworkObject>().Despawn(true);
        }

        foreach (TrapData trapData in _notChoosedTrap)
        {
            ATrap tempTrap = GetTrapFromNetworkId(trapData.networkObjectId);

            tempTrap.gameObject.GetComponent<NetworkObject>().Despawn(true);
        }
    }
}
