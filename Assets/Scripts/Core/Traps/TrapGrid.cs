using System;
using System.Collections.Generic;
using UnityEngine;

// Server-side record of the placed traps: which trap covers each cell, and where traps may go.
// Replaces the "Trap" tilemap, which stored one TrapTile asset per placed trap and never freed it.
// Plain C#, tested in EditMode (TrapGridTests).
public sealed class TrapGrid
{
    private readonly Func<Vector3Int, bool> _isInZone;
    private readonly Dictionary<Vector3Int, int> _trapAtCell = new();
    private readonly Dictionary<int, List<Vector3Int>> _cellsOfTrap = new();

    // isInZone: true for the cells where traps are allowed (in game, the zone tilemap).
    public TrapGrid(Func<Vector3Int, bool> isInZone)
    {
        _isInZone = isInZone ?? throw new ArgumentNullException(nameof(isInZone));
    }

    public int TrapCount => _cellsOfTrap.Count;

    public bool IsInsideZone(TrapShape shape, Vector3Int origin)
    {
        foreach (Vector3Int cell in shape.GetCells(origin))
        {
            if (!_isInZone(cell))
                return false;
        }
        return true;
    }

    public bool IsFree(TrapShape shape, Vector3Int origin)
    {
        foreach (Vector3Int cell in shape.GetCells(origin))
        {
            if (_trapAtCell.ContainsKey(cell))
                return false;
        }
        return true;
    }

    public bool CanPlace(TrapShape shape, Vector3Int origin)
    {
        return IsInsideZone(shape, origin) && IsFree(shape, origin);
    }

    public bool TryPlace(int trapId, TrapShape shape, Vector3Int origin)
    {
        if (_cellsOfTrap.ContainsKey(trapId) || !CanPlace(shape, origin))
            return false;

        List<Vector3Int> cells = new List<Vector3Int>(shape.GetCells(origin));
        foreach (Vector3Int cell in cells)
            _trapAtCell[cell] = trapId;

        _cellsOfTrap[trapId] = cells;
        return true;
    }

    public bool TryGetTrapAt(Vector3Int cell, out int trapId)
    {
        return _trapAtCell.TryGetValue(cell, out trapId);
    }

    public IReadOnlyList<Vector3Int> GetCellsOf(int trapId)
    {
        return _cellsOfTrap.TryGetValue(trapId, out List<Vector3Int> cells)
            ? cells
            : Array.Empty<Vector3Int>();
    }

    public bool Remove(int trapId)
    {
        if (!_cellsOfTrap.TryGetValue(trapId, out List<Vector3Int> cells))
            return false;

        foreach (Vector3Int cell in cells)
            _trapAtCell.Remove(cell);

        _cellsOfTrap.Remove(trapId);
        return true;
    }

    public void Clear()
    {
        _trapAtCell.Clear();
        _cellsOfTrap.Clear();
    }
}