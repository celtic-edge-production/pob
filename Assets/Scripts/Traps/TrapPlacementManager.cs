using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Tilemaps;

// Server-side trap flow of phase 1: the chest offers one trap per player, each player picks
// one, previews it inside the allowed zone, then places it. Also hides, activates and
// despawns the placed traps for the other phases.
public class TrapPlacementManager : NetworkSingleton<TrapPlacementManager>
{
    [Serializable]
    public struct TrapData : INetworkSerializable, IEquatable<TrapData>
    {
        public int trapId;
        public ulong networkObjectId;

        public TrapData(int trapId, ulong networkObjectId)
        {
            this.trapId = trapId;
            this.networkObjectId = networkObjectId;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref trapId);
            serializer.SerializeValue(ref networkObjectId);
        }

        public bool Equals(TrapData other)
        {
            return trapId == other.trapId && networkObjectId == other.networkObjectId;
        }

        public override bool Equals(object obj)
        {
            return obj is TrapData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(trapId, networkObjectId);
        }
    }

    [SerializeField] private Sprite _sprite;
    [SerializeField] private bool debug = false;
    [SerializeField] PhaseManager _PhaseManager;
    [SerializeField] private float _OffsetSpawn = 100.0f;

    // Server-side record of the cells occupied by placed traps.
    private Tilemap _trapTileMap;
    // The zone where traps may be placed.
    private Tilemap _paternTileMap;
    private ATrap[] _gameObjectArray;

    NetworkVariable<int> _lastId = new NetworkVariable<int>();
    NetworkList<TrapData> _placedTrap = new();
    NetworkList<TrapData> _notPlacedTrap = new();
    NetworkList<TrapData> _notChoosedTrap = new();

    private NetworkVariable<bool> _CanChangePhase = new();

    public event Action OnTrapPlaced;
    public event Action OnTrapRemoved;

    protected override void OnSingletonAwake()
    {
        _trapTileMap = GameObject.Find("Trap").GetComponent<Tilemap>();
        _paternTileMap = GameObject.Find("Background").GetComponent<Tilemap>();
        _gameObjectArray = Resources.LoadAll<ATrap>("Prefabs/Traps/Traps");
    }

    private void OnEnable()
    {
        if (_notPlacedTrap != null)
            _notPlacedTrap.OnListChanged += OnNotPlacedListChangedHandler;
    }

    private void OnDisable()
    {
        if (_notPlacedTrap != null)
            _notPlacedTrap.OnListChanged -= OnNotPlacedListChangedHandler;
    }

    private void OnNotPlacedListChangedHandler(NetworkListEvent<TrapData> changeEvent)
    {
        if (!IsServer || !_CanChangePhase.Value)
            return;

        // Every chest trap has been chosen and every chosen trap has been placed.
        if (changeEvent.Type == NetworkListEvent<TrapData>.EventType.Remove &&
            _notPlacedTrap.Count == 0 && _notChoosedTrap.Count == 0)
        {
            _CanChangePhase.Value = false;
            _PhaseManager.SwitchGamePhaseServerRpc();
        }
    }

    #region Player actions (RPCs from the clients, sender validated)

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void OnPlayerSelectedATrapServerRpc(int trapId, ulong playerId, ulong cursordId, RpcParams rpcParams = default)
    {
        if (!RpcValidation.SenderMatches(rpcParams, playerId))
            return;

        NetworkObject cursor = GetNetworkObjectById(cursordId);
        if (cursor == null || cursor.OwnerClientId != playerId)
            return;

        // One trap per player.
        if (GetNotPlacedTrapByPlayerId(playerId) != null)
            return;

        ATrap trap = null;
        foreach (TrapData trapData in _notChoosedTrap)
        {
            ATrap candidate = GetTrapFromNetworkId(trapData.networkObjectId);
            if (candidate != null && candidate.trapId.Value == trapId)
            {
                trap = candidate;
                break;
            }
        }

        if (trap == null)
            return;

        trap.creatorId = playerId;
        trap.needToBeSelected.Value = false;

        _notPlacedTrap.Add(new TrapData(trap.trapId.Value, trap.NetworkObjectId));

        foreach (TrapData trapData in _notChoosedTrap)
        {
            if (trapData.trapId == trapId)
            {
                _notChoosedTrap.Remove(trapData);
                break;
            }
        }

        TilemapHover hover = cursor.GetComponent<TilemapHover>();
        hover.isSettingTrap.Value = true;
        hover.canPlaceTrap.Value = true;

        CloseChestIfEveryoneChose();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void CheckMouseOnTileServerRpc(Vector3 worldPosition, ulong playerId, RpcParams rpcParams = default)
    {
        if (!RpcValidation.SenderMatches(rpcParams, playerId))
            return;

        ATrap trap = GetNotPlacedTrapByPlayerId(playerId);
        if (trap == null || trap.needToBeSelected.Value)
            return;

        Vector3Int cellPosition = _paternTileMap.WorldToCell(worldPosition);

        // The preview only follows the mouse where the whole shape fits in the allowed zone.
        if (!IsShapeInsideZone(cellPosition, trap))
            return;

        if (trap.hasPreviewCell && trap.cellPosition == cellPosition)
            return;

        if (debug)
            UnDrawLastTiles(trap);

        trap.cellPosition = cellPosition;
        trap.hasPreviewCell = true;
        DrawShapeOfTrap(cellPosition, trap);
        trap.transform.position = GetPreviewPosition(cellPosition, trap);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void OnPlayerRotateTrapServerRpc(int position, ulong playerId, RpcParams rpcParams = default)
    {
        if (!RpcValidation.SenderMatches(rpcParams, playerId))
            return;

        ATrap trap = GetNotPlacedTrapByPlayerId(playerId);
        if (trap == null)
            return;

        if (debug)
            UnDrawLastTiles(trap);

        trap.RotateShape(position);
    }

    // The trap is placed where its preview is, never at a position sent by the client:
    // that position could be outside the allowed zone or away from the visible trap.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void OnPlayerPlaceTrapServerRpc(ulong playerId, ulong cursorId, RpcParams rpcParams = default)
    {
        if (!RpcValidation.SenderMatches(rpcParams, playerId))
            return;

        ATrap trap = GetNotPlacedTrapByPlayerId(playerId);
        if (trap == null || !trap.isSetable.Value)
            return;

        if (!PlaceTrap(trap))
            return;

        NetworkObject cursor = GetNetworkObjectById(cursorId);
        if (cursor != null && cursor.OwnerClientId == playerId)
            cursor.GetComponent<TilemapHover>().isSettingTrap.Value = false;

        // May switch to phase 2 if it was the last trap to place.
        foreach (TrapData trapData in _notPlacedTrap)
        {
            if (trapData.trapId == trap.trapId.Value)
            {
                _notPlacedTrap.Remove(trapData);
                break;
            }
        }

        OnTrapPlaced?.Invoke();
    }

    #endregion

    #region Chest flow (server only)

    // Called when the chest UI has opened: one trap per connected player.
    public void CreateTrapsForSelection()
    {
        if (!IsServer)
            return;

        int numberOfPlayers = NetworkManager.Singleton.ConnectedClients.Count;
        for (int i = 0; i < numberOfPlayers; i++)
            CreateTrapToBeSelected();

        _CanChangePhase.Value = true;
    }

    private void CreateTrapToBeSelected()
    {
        GameObject trapChestObject = GameObject.Find("TrapChest(Clone)");
        if (trapChestObject == null)
            return;

        RectTransform chestUiRect = trapChestObject.GetComponent<TrapChest>().Image.GetComponent<RectTransform>();
        if (chestUiRect == null)
            return;

        float halfWidth = chestUiRect.rect.width / 2;
        float halfHeight = chestUiRect.rect.height / 2;
        float spawnWidth = halfWidth - _OffsetSpawn < 0 ? halfWidth : halfWidth - _OffsetSpawn;
        float spawnHeight = halfHeight - _OffsetSpawn < 0 ? halfHeight : halfHeight - _OffsetSpawn;

        ATrap trap = Instantiate(_gameObjectArray[UnityEngine.Random.Range(0, _gameObjectArray.Length)]);
        trap.transform.position = new Vector3(
            UnityEngine.Random.Range(-spawnWidth, spawnWidth) / 100.0f,
            0,
            UnityEngine.Random.Range(-spawnHeight, spawnHeight) / 100.0f);

        trap.NetworkObject.Spawn();
        trap.creatorId = 0;
        trap.trapId.Value = _lastId.Value;

        _notChoosedTrap.Add(new TrapData(trap.trapId.Value, trap.NetworkObjectId));
        _lastId.Value++;
    }

    private void CloseChestIfEveryoneChose()
    {
        if (NetworkManager.Singleton.ConnectedClients.Count != _notPlacedTrap.Count)
            return;

        GameObject trapChest = GameObject.Find("TrapChest(Clone)");
        if (trapChest == null)
            return;

        GameObject chestUi = trapChest.GetComponent<TrapChest>().Image;
        if (chestUi == null)
            return;

        chestUi.GetComponent<ChestUi>().StartClosingAnimation();
        ShowAllPlacedTrapClientRpc();
    }

    // Called when the chest UI has closed: the chosen traps can now be placed.
    public void ToggleSelectedTrap()
    {
        foreach (TrapData trapData in _notPlacedTrap)
        {
            ATrap trap = GetTrapFromNetworkId(trapData.networkObjectId);
            if (trap != null)
                trap.isSetable.Value = true;
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
                trap.NetworkObject.Despawn(true);
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
                spareTrap.NetworkObject.Despawn(true);

            CloseChestIfEveryoneChose();
        }

        // Case 3: his trap was already placed. Nothing to do, it stays on the map.
    }

    #endregion

    #region Grid

    private static IEnumerable<Vector3Int> GetShapeCells(Vector3Int origin, ATrap trap)
    {
        for (int i = 0; i < trap.matrix.GetLength(0); i++)
        {
            for (int j = 0; j < trap.matrix.GetLength(1); j++)
            {
                if (trap.matrix[i, j] == 1)
                    yield return new Vector3Int(origin.x + j, origin.y - i, 0);
            }
        }
    }

    private bool IsShapeInsideZone(Vector3Int origin, ATrap trap)
    {
        foreach (Vector3Int cell in GetShapeCells(origin, trap))
        {
            if (!_paternTileMap.HasTile(cell))
                return false;
        }
        return true;
    }

    private bool IsAreaFreeOfTraps(Vector3Int origin, ATrap trap)
    {
        foreach (Vector3Int cell in GetShapeCells(origin, trap))
        {
            if (_trapTileMap.HasTile(cell))
                return false;
        }
        return true;
    }

    private bool PlaceTrap(ATrap trap)
    {
        if (!trap.hasPreviewCell || trap.isPlaced.Value)
            return false;

        Vector3Int origin = trap.cellPosition;

        // Re-checked here: a rotation since the last preview may have changed the shape.
        if (!IsShapeInsideZone(origin, trap))
            return false;

        if (trap.isBombTrap)
        {
            ((BombTrap)trap).ExplodeBomb(origin);
            return true;
        }

        if (!IsAreaFreeOfTraps(origin, trap))
            return false;

        TrapTile trapTile = ScriptableObject.CreateInstance<TrapTile>();
        trapTile.trapPrefab = trap;
        trapTile.id = trap.trapId.Value;

        if (debug)
            trapTile.sprite = _sprite;

        foreach (Vector3Int cell in GetShapeCells(origin, trap))
            _trapTileMap.SetTile(cell, trapTile);

        if (debug)
            UnDrawLastTiles(trap);

        trap.isPlaced.Value = true;
        _placedTrap.Add(new TrapData(trap.trapId.Value, trap.NetworkObjectId));
        return true;
    }

    // Server only. Removes the placed trap covering this cell (used by the bomb trap).
    public void RemoveTrapAt(Vector3Int cellPosition)
    {
        if (!IsServer)
            return;

        if (_trapTileMap.GetTile(cellPosition) is not TrapTile trapTile)
            return;

        int id = trapTile.id;

        foreach (TrapData trapData in _placedTrap)
        {
            if (trapData.trapId == id)
            {
                _placedTrap.Remove(trapData);
                break;
            }
        }

        foreach (Vector3Int cell in _trapTileMap.cellBounds.allPositionsWithin)
        {
            if (_trapTileMap.GetTile(cell) is TrapTile tile && tile.id == id)
                _trapTileMap.SetTile(cell, null);
        }

        if (trapTile.trapPrefab != null && trapTile.trapPrefab.IsSpawned)
            trapTile.trapPrefab.NetworkObject.Despawn(true);

        OnTrapRemoved?.Invoke();
    }

    private static Vector3 GetPreviewPosition(Vector3Int cellPosition, ATrap trap)
    {
        float sizeX = trap.matrix.GetLength(1);
        float sizeY = trap.matrix.GetLength(0);

        if (sizeX > 1 && sizeY > 1)
        {
            float offsetY = (sizeY % 2 == 0) ? sizeY / 2 - 1.0f : sizeY / 2 - 0.5f;
            return cellPosition + new Vector3(sizeX / 2, -offsetY, 0);
        }

        if (sizeX > 1)
            return cellPosition + new Vector3(sizeX / 2, 0.5f, 0);

        if (sizeY > 1)
        {
            float offsetY = (sizeY % 2 == 0) ? sizeY / 2 - 2.0f : sizeY / 2 - 1.0f;
            return cellPosition + new Vector3(0.5f, -offsetY, 0);
        }

        return cellPosition + new Vector3(0.5f, 0.5f, 0);
    }

    private void UnDrawLastTiles(ATrap trap)
    {
        foreach (Vector3Int cell in GetShapeCells(trap.cellPosition, trap))
        {
            if (_paternTileMap.HasTile(cell))
            {
                _paternTileMap.SetTileFlags(cell, TileFlags.None);
                _paternTileMap.SetColor(cell, Color.white);
            }
        }
    }

    private void DrawShapeOfTrap(Vector3Int cellPosition, ATrap trap)
    {
        if (!debug)
            return;

        foreach (Vector3Int cell in GetShapeCells(cellPosition, trap))
        {
            if (_paternTileMap.HasTile(cell))
            {
                _paternTileMap.SetTileFlags(cell, TileFlags.None);
                _paternTileMap.SetColor(cell, Color.green);
            }
        }
    }

    #endregion

    #region Placed traps lifecycle (server only)

    // Safe to call from any peer: only the server broadcasts.
    public void HideAllPlacedTraps()
    {
        if (IsServer)
            HideAllPlacedTrapClientRpc();
    }

    public void ActivateTraps()
    {
        foreach (TrapData trapData in _placedTrap)
        {
            ATrap trap = GetTrapFromNetworkId(trapData.networkObjectId);
            if (trap != null)
                trap.ActivateCollideStateClientRpc();
        }
    }

    public void DeactivateTraps()
    {
        foreach (TrapData trapData in _placedTrap)
        {
            ATrap trap = GetTrapFromNetworkId(trapData.networkObjectId);
            if (trap != null)
                trap.UnActivateCollideStateClientRpc();
        }
    }

    public void DespawnAllTraps()
    {
        DespawnTraps(_placedTrap);
        DespawnTraps(_notPlacedTrap);
        DespawnTraps(_notChoosedTrap);
    }

    private void DespawnTraps(NetworkList<TrapData> traps)
    {
        foreach (TrapData trapData in traps)
        {
            ATrap trap = GetTrapFromNetworkId(trapData.networkObjectId);
            if (trap != null && trap.IsSpawned)
                trap.NetworkObject.Despawn(true);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void HideAllPlacedTrapClientRpc()
    {
        SetPlacedTrapsVisible(false);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowAllPlacedTrapClientRpc()
    {
        SetPlacedTrapsVisible(true);
    }

    private void SetPlacedTrapsVisible(bool visible)
    {
        foreach (TrapData trapData in _placedTrap)
        {
            ATrap trap = GetTrapFromNetworkId(trapData.networkObjectId);
            if (trap != null)
                trap.gameObject.SetActive(visible);
        }
    }

    #endregion

    #region Lookups

    private NetworkObject GetNetworkObjectById(ulong networkObjectId)
    {
        return NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var networkObject)
            ? networkObject
            : null;
    }

    private ATrap GetTrapFromNetworkId(ulong networkObjectId)
    {
        NetworkObject networkObject = GetNetworkObjectById(networkObjectId);
        return networkObject != null ? networkObject.GetComponent<ATrap>() : null;
    }

    private ATrap GetNotPlacedTrapByPlayerId(ulong playerId)
    {
        foreach (TrapData trapData in _notPlacedTrap)
        {
            ATrap trap = GetTrapFromNetworkId(trapData.networkObjectId);
            if (trap != null && trap.creatorId == playerId)
                return trap;
        }
        return null;
    }

    #endregion
}