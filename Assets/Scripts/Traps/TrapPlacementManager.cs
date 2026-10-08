using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Tilemaps;

// Server-side trap flow of phase 1: the chest offers one trap per player, each player picks
// one, previews it inside the allowed zone, then places it. Also hides, activates and
// despawns the placed traps for the other phases. The chest sequence itself (open, wait for
// every choice, close) is run by TrapSelection.
// Each trap carries its own TrapStage; this manager only keeps the list of the traps it spawned.
// While a player holds a trap he owns it and moves it himself (TilemapHover): the server only
// tells him whether the preview is valid, and checks everything again when he places it.
public class TrapPlacementManager : NetworkSingleton<TrapPlacementManager>
{
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

    // Server only: every trap spawned this game, whatever its stage.
    private readonly List<ATrap> _traps = new();
    private int _nextTrapId;

    // Server only: true from the moment the chest offers its traps until the last one is placed.
    private bool _isPlacementPhaseRunning;

    // Server only, debug colours: the cells each preview last coloured.
    private readonly Dictionary<ATrap, List<Vector3Int>> _debugPreviewCells = new();

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

        // Traps may go on every painted cell of the zone tilemap: walls must be left unpainted.
        _grid = new TrapGrid(cell => _paternTileMap.HasTile(cell));

        _gameObjectArray = Resources.LoadAll<ATrap>("Prefabs/Traps/Traps");
    }

    // Valid on every peer: clients use it to send the preview only when the hovered cell changes.
    public Vector3Int WorldToCell(Vector3 worldPosition)
    {
        return _paternTileMap.WorldToCell(worldPosition);
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
        if (GetHeldTrapOf(playerId) != null)
            return;

        ATrap trap = FindTrap(t => t.trapId.Value == trapId && t.Stage.Value == TrapStage.InChest);
        if (trap == null)
            return;

        trap.ChosenBy = playerId;
        trap.TrySetStage(TrapStage.Chosen);

        // The player moves his trap himself until he places it. If he leaves, the server gets it
        // back instead of NGO destroying it, and HandlePlayerLeft removes it.
        trap.NetworkObject.DontDestroyWithOwner = true;
        trap.NetworkObject.ChangeOwnership(playerId);

        TilemapHover hover = cursor.GetComponent<TilemapHover>();
        hover.isSettingTrap.Value = true;
        hover.canPlaceTrap.Value = true;
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void MuteTrapDestroySoundsClientRpc()
    {
        ATrap.MuteDestroySound = true;
    }

    // Sent by the placing player when his preview changes cell or orientation: the trap already moved
    // on his side, the server only answers whether it could be placed there (red tint otherwise).
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void OnPlayerPreviewChangedServerRpc(int cellX, int cellY, byte quarterTurns, bool mirrored, ulong playerId, RpcParams rpcParams = default)
    {
        if (!RpcValidation.SenderMatches(rpcParams, playerId))
            return;

        ATrap trap = GetHeldTrapOf(playerId);
        if (trap == null)
            return;

        Vector3Int cell = new Vector3Int(cellX, cellY, 0);
        TrapShape shape = trap.GetShape(new TrapOrientation(quarterTurns, mirrored));

        trap.isPreviewValid.Value = CanPlaceAt(trap, shape, cell);

        if (debug)
            DrawPreview(trap, shape, cell);
    }

    // The cell and orientation come from the player, so everything is checked again here; the trap's
    // final transform is then set by the server, whatever the client showed.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void OnPlayerPlaceTrapServerRpc(int cellX, int cellY, byte quarterTurns, bool mirrored, ulong playerId, ulong cursorId, RpcParams rpcParams = default)
    {
        if (!RpcValidation.SenderMatches(rpcParams, playerId))
            return;

        ATrap trap = GetHeldTrapOf(playerId);
        if (trap == null || trap.Stage.Value != TrapStage.Placing)
            return;

        if (!PlaceTrap(trap, new Vector3Int(cellX, cellY, 0), new TrapOrientation(quarterTurns, mirrored)))
            return;

        NetworkObject cursor = GetNetworkObjectById(cursorId);
        if (cursor != null && cursor.OwnerClientId == playerId)
            cursor.GetComponent<TilemapHover>().isSettingTrap.Value = false;

        OnTrapPlaced?.Invoke();

        // May switch to phase 2 if it was the last trap to place.
        EndPlacementPhaseIfDone();
    }

    #endregion

    #region Chest flow (server only)

    // Server only (TrapSelection): the chest offers one trap per connected player, spread over its UI.
    public void CreateTrapsForSelection(RectTransform spawnArea)
    {
        if (!IsServer)
            return;

        int numberOfPlayers = NetworkManager.Singleton.ConnectedClients.Count;
        for (int i = 0; i < numberOfPlayers; i++)
            CreateTrapToBeSelected(spawnArea);

        _isPlacementPhaseRunning = true;
    }

    private void CreateTrapToBeSelected(RectTransform spawnArea)
    {
        float halfWidth = spawnArea.rect.width / 2;
        float halfHeight = spawnArea.rect.height / 2;
        float spawnWidth = halfWidth - _OffsetSpawn < 0 ? halfWidth : halfWidth - _OffsetSpawn;
        float spawnHeight = halfHeight - _OffsetSpawn < 0 ? halfHeight : halfHeight - _OffsetSpawn;

        ATrap trap = Instantiate(_gameObjectArray[UnityEngine.Random.Range(0, _gameObjectArray.Length)]);
        trap.transform.position = new Vector3(
            UnityEngine.Random.Range(-spawnWidth, spawnWidth) / 100.0f,
            0,
            UnityEngine.Random.Range(-spawnHeight, spawnHeight) / 100.0f);

        trap.NetworkObject.Spawn();
        trap.trapId.Value = _nextTrapId++;
        _traps.Add(trap);
    }

    // Server only (TrapSelection): the chest can close once no trap is left in it. A player who leaves
    // before choosing takes his spare trap with him (HandlePlayerLeft), so the others never wait for him.
    public bool EveryoneHasChosen
    {
        get
        {
            if (!_isPlacementPhaseRunning)
                return false;

            foreach (ATrap trap in Traps())
            {
                if (trap.Stage.Value == TrapStage.InChest)
                    return false;
            }
            return true;
        }
    }

    // Server only (TrapSelection): the chest has closed, the chosen traps can now be placed.
    public void MakeChosenTrapsPlaceable()
    {
        foreach (ATrap trap in Traps())
        {
            if (trap.Stage.Value == TrapStage.Chosen)
                trap.TrySetStage(TrapStage.Placing);
        }

        // Nothing to place (every holder left meanwhile): phase 2 starts right away.
        EndPlacementPhaseIfDone();
    }

    public void HandlePlayerLeft(ulong clientId)
    {
        if (!IsServer)
            return;

        // Case 1: the player held a trap he had not placed. It would block the phase forever.
        ATrap heldTrap = GetHeldTrapOf(clientId);
        if (heldTrap != null)
        {
            Despawn(heldTrap);
            EndPlacementPhaseIfDone();
            return;
        }

        // Case 2: the player had not chosen yet. The chest holds one trap per player, so drop a
        // spare one: TrapSelection then sees that everyone left has chosen.
        ATrap spare = FindTrap(t => t.Stage.Value == TrapStage.InChest);
        if (spare != null)
            Despawn(spare);

        // Case 3: his trap was already placed. Nothing to do, it stays on the map.
    }

    // Server only: phase 2 starts once no trap is waiting for a player any more.
    private void EndPlacementPhaseIfDone()
    {
        if (!_isPlacementPhaseRunning)
            return;

        foreach (ATrap trap in Traps())
        {
            if (TrapStageRules.IsPending(trap.Stage.Value))
                return;
        }

        _isPlacementPhaseRunning = false;
        _PhaseManager.SwitchGamePhaseServerRpc();
    }

    #endregion

    #region Grid

    // The bomb trap goes on top of another trap (it destroys it): it only needs the zone.
    private bool CanPlaceAt(ATrap trap, TrapShape shape, Vector3Int origin)
    {
        return trap.isBombTrap
            ? _grid.IsInsideZone(shape, origin)
            : _grid.CanPlace(shape, origin);
    }

    private bool PlaceTrap(ATrap trap, Vector3Int origin, TrapOrientation orientation)
    {
        TrapShape shape = trap.GetShape(orientation);

        if (!_grid.IsInsideZone(shape, origin))
            return false;

        if (trap.isBombTrap)
        {
            // Never on the grid: it removes the trap below and disappears (BombTrap.ExplodeBomb).
            ReturnToServer(trap, origin, orientation);
            trap.TrySetStage(TrapStage.Placed);
            ((BombTrap)trap).ExplodeBomb(origin);
            return true;
        }

        if (!_grid.TryPlace(trap.trapId.Value, shape, origin))
            return false;

        ReturnToServer(trap, origin, orientation);
        trap.TrySetStage(TrapStage.Placed);

        if (debug)
            ColorCells(_grid.GetCellsOf(trap.trapId.Value), Color.red);

        return true;
    }

    // The placed trap belongs to the server again, which sets its exact final transform.
    private void ReturnToServer(ATrap trap, Vector3Int origin, TrapOrientation orientation)
    {
        trap.NetworkObject.RemoveOwnership();
        trap.cellPosition = origin;
        trap.ShowAt(origin, orientation);
        trap.isPreviewValid.Value = true;
        _debugPreviewCells.Remove(trap);
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

        ATrap removedTrap = FindTrap(t => t.trapId.Value == trapId);
        if (removedTrap != null)
            Despawn(removedTrap);

        OnTrapRemoved?.Invoke();
    }

    // Debug only: the preview (green) and the placed traps (red) as colours on the zone tilemap.
    private void DrawPreview(ATrap trap, TrapShape shape, Vector3Int origin)
    {
        if (_debugPreviewCells.TryGetValue(trap, out List<Vector3Int> previous))
            ColorCells(previous, Color.white);

        List<Vector3Int> cells = new List<Vector3Int>(shape.GetCells(origin));
        _debugPreviewCells[trap] = cells;
        ColorCells(cells, Color.green);
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

    public void ShowAllPlacedTraps()
    {
        if (IsServer)
            ShowAllPlacedTrapClientRpc();
    }

    public void ActivateTraps()
    {
        foreach (ATrap trap in Traps())
        {
            if (trap.Stage.Value == TrapStage.Placed)
                trap.TrySetStage(TrapStage.Active);
        }
    }

    public void DeactivateTraps()
    {
        foreach (ATrap trap in Traps())
        {
            if (trap.Stage.Value == TrapStage.Active)
                trap.TrySetStage(TrapStage.Placed);
        }
    }

    public void DespawnAllTraps()
    {
        MuteTrapDestroySoundsClientRpc();
        _grid.Clear();

        foreach (ATrap trap in Traps())
            trap.NetworkObject.Despawn(true);

        _traps.Clear();
        _debugPreviewCells.Clear();
        _isPlacementPhaseRunning = false;
    }

    // Clients know nothing of the server list: they look at every trap's own stage.
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

    private static void SetPlacedTrapsVisible(bool visible)
    {
        // Inactive traps included: they are the ones to show again.
        foreach (ATrap trap in FindObjectsByType<ATrap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (trap.IsSpawned && TrapStageRules.IsOnMap(trap.Stage.Value))
                trap.gameObject.SetActive(visible);
        }
    }

    #endregion

    #region Lookups

    // Server only: the spawned traps, despawned ones dropped on the way (bomb trap, removed traps).
    private List<ATrap> Traps()
    {
        _traps.RemoveAll(trap => trap == null || !trap.IsSpawned);
        return new List<ATrap>(_traps);
    }

    private ATrap FindTrap(Predicate<ATrap> match)
    {
        foreach (ATrap trap in Traps())
        {
            if (match(trap))
                return trap;
        }
        return null;
    }

    private int CountTraps(TrapStage stage)
    {
        int count = 0;
        foreach (ATrap trap in Traps())
        {
            if (trap.Stage.Value == stage)
                count++;
        }
        return count;
    }

    // The trap this player has chosen and not placed yet, if any.
    private ATrap GetHeldTrapOf(ulong playerId)
    {
        return FindTrap(t => TrapStageRules.IsHeld(t.Stage.Value) && t.ChosenBy == playerId);
    }

    private void Despawn(ATrap trap)
    {
        _traps.Remove(trap);
        _debugPreviewCells.Remove(trap);
        if (trap.IsSpawned)
            trap.NetworkObject.Despawn(true);
    }

    private NetworkObject GetNetworkObjectById(ulong networkObjectId)
    {
        return NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var networkObject)
            ? networkObject
            : null;
    }

    #endregion
}