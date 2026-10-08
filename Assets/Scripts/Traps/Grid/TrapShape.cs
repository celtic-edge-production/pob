using System;
using System.Collections.Generic;
using UnityEngine;

// The cells a trap covers on the grid, relative to its origin cell.
// Row 0 is the top row and the origin is the top-left cell of the bounding box: column c and
// row r land on (origin.x + c, origin.y - r). Immutable: a rotation returns a new shape.
// Plain C#, tested in EditMode (TrapGridTests).
public sealed class TrapShape
{
    // [row, column], 1 = covered.
    private readonly int[,] _matrix;

    public int Width => _matrix.GetLength(1);
    public int Height => _matrix.GetLength(0);

    public TrapShape(int[,] matrix)
    {
        if (matrix == null)
            throw new ArgumentNullException(nameof(matrix));

        _matrix = (int[,])matrix.Clone();
    }

    public bool Covers(int row, int column)
    {
        return _matrix[row, column] == 1;
    }

    public int[,] ToMatrix()
    {
        return (int[,])_matrix.Clone();
    }

    // Quarter turns counterclockwise (the same way as transform.Rotate on z); negative turns clockwise.
    public TrapShape Rotated(int quarterTurns)
    {
        int steps = ((quarterTurns % 4) + 4) % 4;

        int[,] result = _matrix;
        for (int i = 0; i < steps; i++)
            result = RotateOnceCounterclockwise(result);

        return new TrapShape(result);
    }

    private static int[,] RotateOnceCounterclockwise(int[,] source)
    {
        int height = source.GetLength(0);
        int width = source.GetLength(1);
        int[,] rotated = new int[width, height];

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                rotated[width - x - 1, y] = source[y, x];

        return rotated;
    }

    public IEnumerable<Vector3Int> GetCells(Vector3Int origin)
    {
        for (int row = 0; row < Height; row++)
        {
            for (int column = 0; column < Width; column++)
            {
                if (_matrix[row, column] == 1)
                    yield return new Vector3Int(origin.x + column, origin.y - row, 0);
            }
        }
    }

    // Offset from the origin cell to the trap's transform, so that its sprite covers the shape.
    public Vector3 GetPreviewOffset()
    {
        float sizeX = Width;
        float sizeY = Height;

        if (sizeX > 1 && sizeY > 1)
        {
            float offsetY = (sizeY % 2 == 0) ? sizeY / 2 - 1.0f : sizeY / 2 - 0.5f;
            return new Vector3(sizeX / 2, -offsetY, 0);
        }

        if (sizeX > 1)
            return new Vector3(sizeX / 2, 0.5f, 0);

        if (sizeY > 1)
        {
            float offsetY = (sizeY % 2 == 0) ? sizeY / 2 - 2.0f : sizeY / 2 - 1.0f;
            return new Vector3(0.5f, -offsetY, 0);
        }

        return new Vector3(0.5f, 0.5f, 0);
    }
}