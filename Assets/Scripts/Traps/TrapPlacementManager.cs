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

    [Tooltip("Colours the preview (green) and the occupied cells (red) on the zone tilemap.")]
    [SerializeField] private bool debug = false;
    [SerializeField] PhaseManager _PhaseManager;
    [SerializeField] private float _OffsetSpawn = 100.0f;

    [Header("Tilemaps")]
    [Tooltip("The zone where traps may be placed.")]
    [SerializeField] private Tilemap _paternTileMap;
    private ATrap[] _gameObjectArray;

    // Server only: which placed trap covers each cell.
    private TrapGrid _grid;

    NetworkVariable<int> _lastId = new NetworkVariable<int>();
    NetworkList<TrapData> _placedTrap = new();
    NetworkList<TrapData> _notPlacedTrap = new();
    NetworkList<TrapData> _notChoosedTrap = new();

    private NetworkVariable<bool> _CanChangePhase = new();

    public event Action OnTrapPlaced;
    public event Action OnTrapRemoved;

    protected override void OnSingletonAwake()
    {
        // Fallback for levels where the zone tilemap is not assigned in the Inspector yet.
        if (_paternTileMap == null)
        {
            _paternTileMap = GameObject.Find("Background").GetComponent<Tilemap>();
            Debug.LogWarning("[TrapPlacementManager] Assign the zone tilemap in the Inspector.");
        }

        _grid = new TrapGrid(cell => _paternTileMap.HasTile(cell));

        _gameObjectArray = Resources.LoadAll<ATrap>("Prefabs/Traps/Traps");
    }

    // Valid on every peer: clients use it to send the preview only when the hovered cell changes.
    public Vector3Int WorldToCell(Vector3 worldPosition)
    {
        return _paternTileMap.WorldToCell(worldPosition);
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

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void MuteTrapDestroySoundsClientRpc()
    {
        ATrap.MuteDestroySound = true;
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
        if (!_grid.IsInsideZone(trap.Shape, cellPosition))
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
        // Re-centre the preview for the new shape. If it no longer fits here, it stays put
        // and PlaceTrap refuses it until the player moves to a cell where it fits.
        if (trap.hasPreviewCell && _grid.IsInsideZone(trap.Shape, trap.cellPosition))
            trap.transform.position = GetPreviewPosition(trap.cellPosition, trap);
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
        if (TrapChest.Current == null || TrapChest.Current.Image == null)
            return;

        RectTransform chestUiRect = TrapChest.Current.Image.GetComponent<RectTransform>();
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

        if (TrapChest.Current == null || TrapChest.Current.Image == null)
            return;

        TrapChest.Current.Image.GetComponent<ChestUi>().StartClosingAnimation();
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

    private bool PlaceTrap(ATrap trap)
    {
        if (!trap.hasPreviewCell || trap.isPlaced.Value)
            return false;

        Vector3Int origin = trap.cellPosition;

        // Re-checked here: a rotation since the last preview may have changed the shape.
        if (!_grid.IsInsideZone(trap.Shape, origin))
            return false;

        if (trap.isBombTrap)
        {
            ((BombTrap)trap).ExplodeBomb(origin);
            return true;
        }

        if (!_grid.TryPlace(trap.trapId.Value, trap.Shape, origin))
            return false;

        if (debug)
            ColorCells(_grid.GetCellsOf(trap.trapId.Value), Color.red);

        trap.isPlaced.Value = true;
        _placedTrap.Add(new TrapData(trap.trapId.Value, trap.NetworkObjectId));
        return true;
    }

    // Server only. Removes the placed trap covering this cell (used by the bomb trap).
    public void RemoveTrapAt(Vector3Int cellPosition)
    {
        if (!IsServer)
            return;

        if (!_grid.TryGetTrapAt(cellPosition, out int trapId))
            return;

        if (debug)
            ColorCells(_grid.GetCellsOf(trapId), Color.white);

        _grid.Remove(trapId);

        ATrap removedTrap = null;
        foreach (TrapData trapData in _placedTrap)
        {
            if (trapData.trapId == trapId)
            {
                removedTrap = GetTrapFromNetworkId(trapData.networkObjectId);
                _placedTrap.Remove(trapData);
                break;
            }
        }

        if (removedTrap != null && removedTrap.IsSpawned)
            removedTrap.NetworkObject.Despawn(true);

        OnTrapRemoved?.Invoke();
    }

    private static Vector3 GetPreviewPosition(Vector3Int cellPosition, ATrap trap)
    {
        return cellPosition + trap.Shape.GetPreviewOffset();
    }

    // Debug only: the preview and the placed traps are drawn as colours on the zone tilemap.
    private void UnDrawLastTiles(ATrap trap)
    {
        ColorCells(trap.Shape.GetCells(trap.cellPosition), Color.white);
    }

    private void DrawShapeOfTrap(Vector3Int cellPosition, ATrap trap)
    {
        if (debug)
            ColorCells(trap.Shape.GetCells(cellPosition), Color.green);
    }

    private void ColorCells(IEnumerable<Vector3Int> cells, Color color)
    {
        foreach (Vector3Int cell in cells)
        {
            if (_paternTileMap.HasTile(cell))
            {
                _paternTileMap.SetTileFlags(cell, TileFlags.None);
                _paternTileMap.SetColor(cell, color);
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
        MuteTrapDestroySoundsClientRpc();
        _grid.Clear();
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

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void HideAllPlacedTrapClientRpc()
    {
        SetPlacedTrapsVisible(false);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
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