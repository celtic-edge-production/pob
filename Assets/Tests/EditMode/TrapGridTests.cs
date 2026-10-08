using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class TrapShapeTests
{
    // Distinct values make every move of the rotation visible (only 1 counts as covered).
    private static readonly int[,] Asymmetric = { { 1, 2, 3 }, { 4, 5, 6 } };

    [Test]
    public void GetCells_SingleCell_IsTheOrigin()
    {
        TrapShape shape = new TrapShape(new int[,] { { 1 } });

        CollectionAssert.AreEquivalent(new[] { new Vector3Int(4, 7, 0) }, shape.GetCells(new Vector3Int(4, 7, 0)));
    }

    [Test]
    public void GetCells_ColumnsGoRightAndRowsGoDown()
    {
        TrapShape shape = new TrapShape(new int[,] { { 1, 0 }, { 1, 1 } });

        CollectionAssert.AreEquivalent(
            new[] { new Vector3Int(5, 5, 0), new Vector3Int(5, 4, 0), new Vector3Int(6, 4, 0) },
            shape.GetCells(new Vector3Int(5, 5, 0)));
    }

    // The three expected matrices are the ones the former ATrap.RotateShape produced.
    [Test]
    public void Rotated_OneQuarterTurn_MatchesFormerRotation()
    {
        Assert.AreEqual(new int[,] { { 3, 6 }, { 2, 5 }, { 1, 4 } }, new TrapShape(Asymmetric).Rotated(1).ToMatrix());
    }

    [Test]
    public void Rotated_HalfTurn_MatchesFormerRotation()
    {
        Assert.AreEqual(new int[,] { { 6, 5, 4 }, { 3, 2, 1 } }, new TrapShape(Asymmetric).Rotated(2).ToMatrix());
    }

    [Test]
    public void Rotated_ThreeQuarterTurns_MatchesFormerRotation()
    {
        Assert.AreEqual(new int[,] { { 4, 1 }, { 5, 2 }, { 6, 3 } }, new TrapShape(Asymmetric).Rotated(3).ToMatrix());
    }

    [Test]
    public void Rotated_NegativeTurn_IsTheOppositeDirection()
    {
        TrapShape shape = new TrapShape(Asymmetric);

        Assert.AreEqual(shape.Rotated(3).ToMatrix(), shape.Rotated(-1).ToMatrix());
    }

    [Test]
    public void Rotated_FullTurn_GivesTheSameShape()
    {
        Assert.AreEqual(Asymmetric, new TrapShape(Asymmetric).Rotated(4).ToMatrix());
    }

    [Test]
    public void Rotated_LeavesTheOriginalUnchanged()
    {
        TrapShape shape = new TrapShape(Asymmetric);

        shape.Rotated(1);

        Assert.AreEqual(Asymmetric, shape.ToMatrix());
    }

    [Test]
    public void Constructor_CopiesTheMatrix()
    {
        int[,] matrix = { { 1 } };
        TrapShape shape = new TrapShape(matrix);

        matrix[0, 0] = 0;

        Assert.IsTrue(shape.Covers(0, 0));
    }

    [TestCase(1, 1, 0.5f, 0.5f)]
    [TestCase(3, 1, 1.5f, 0.5f)]
    [TestCase(1, 3, 0.5f, -0.5f)]
    [TestCase(1, 2, 0.5f, 1.0f)]
    [TestCase(2, 2, 1.0f, 0.0f)]
    [TestCase(2, 3, 1.0f, -1.0f)]
    public void GetPreviewOffset_MatchesFormerFormula(int width, int height, float expectedX, float expectedY)
    {
        int[,] matrix = new int[height, width];
        TrapShape shape = new TrapShape(matrix);

        Vector3 offset = shape.GetPreviewOffset();

        Assert.AreEqual(expectedX, offset.x, 1e-5f);
        Assert.AreEqual(expectedY, offset.y, 1e-5f);
    }
}

public class TrapGridTests
{
    private static readonly TrapShape Single = new TrapShape(new int[,] { { 1 } });
    private static readonly TrapShape Row3 = new TrapShape(new int[,] { { 1, 1, 1 } });

    // Allowed zone: a 10 x 10 square from (0, 0) to (9, 9).
    private static TrapGrid CreateGrid()
    {
        return new TrapGrid(cell => cell.x >= 0 && cell.x < 10 && cell.y >= 0 && cell.y < 10);
    }

    [Test]
    public void CanPlace_InsideAnEmptyZone_IsTrue()
    {
        Assert.IsTrue(CreateGrid().CanPlace(Row3, new Vector3Int(0, 0, 0)));
    }

    [Test]
    public void IsInsideZone_ShapeCrossingTheEdge_IsFalse()
    {
        Assert.IsFalse(CreateGrid().IsInsideZone(Row3, new Vector3Int(8, 0, 0)));
    }

    [Test]
    public void TryPlace_OutsideTheZone_Fails()
    {
        TrapGrid grid = CreateGrid();

        Assert.IsFalse(grid.TryPlace(1, Row3, new Vector3Int(8, 0, 0)));
        Assert.AreEqual(0, grid.TrapCount);
    }

    [Test]
    public void TryPlace_OverlappingAnotherTrap_Fails()
    {
        TrapGrid grid = CreateGrid();
        grid.TryPlace(1, Row3, new Vector3Int(0, 0, 0));

        Assert.IsFalse(grid.TryPlace(2, Single, new Vector3Int(2, 0, 0)));
    }

    [Test]
    public void TryPlace_NextToAnotherTrap_Succeeds()
    {
        TrapGrid grid = CreateGrid();
        grid.TryPlace(1, Row3, new Vector3Int(0, 0, 0));

        Assert.IsTrue(grid.TryPlace(2, Single, new Vector3Int(3, 0, 0)));
        Assert.AreEqual(2, grid.TrapCount);
    }

    [Test]
    public void TryPlace_SameTrapTwice_Fails()
    {
        TrapGrid grid = CreateGrid();
        grid.TryPlace(1, Single, new Vector3Int(0, 0, 0));

        Assert.IsFalse(grid.TryPlace(1, Single, new Vector3Int(5, 5, 0)));
    }

    [Test]
    public void TryGetTrapAt_FindsTheTrapOnEveryCoveredCell()
    {
        TrapGrid grid = CreateGrid();
        grid.TryPlace(7, Row3, new Vector3Int(2, 4, 0));

        foreach (int x in new[] { 2, 3, 4 })
        {
            Assert.IsTrue(grid.TryGetTrapAt(new Vector3Int(x, 4, 0), out int trapId));
            Assert.AreEqual(7, trapId);
        }

        Assert.IsFalse(grid.TryGetTrapAt(new Vector3Int(5, 4, 0), out _));
    }

    [Test]
    public void TryPlace_RotatedShape_CoversTheRotatedCells()
    {
        TrapGrid grid = CreateGrid();

        // Row of 3 turned a quarter: a column of 3 going down from the origin.
        grid.TryPlace(1, Row3.Rotated(1), new Vector3Int(5, 5, 0));

        CollectionAssert.AreEquivalent(
            new[] { new Vector3Int(5, 5, 0), new Vector3Int(5, 4, 0), new Vector3Int(5, 3, 0) },
            grid.GetCellsOf(1).ToArray());
    }

    [Test]
    public void Remove_FreesItsCells()
    {
        TrapGrid grid = CreateGrid();
        grid.TryPlace(1, Row3, new Vector3Int(0, 0, 0));

        Assert.IsTrue(grid.Remove(1));
        Assert.IsTrue(grid.IsFree(Row3, new Vector3Int(0, 0, 0)));
        Assert.AreEqual(0, grid.GetCellsOf(1).Count);
    }

    [Test]
    public void Remove_UnknownTrap_ReturnsFalse()
    {
        Assert.IsFalse(CreateGrid().Remove(42));
    }

    [Test]
    public void Clear_RemovesEveryTrap()
    {
        TrapGrid grid = CreateGrid();
        grid.TryPlace(1, Single, new Vector3Int(0, 0, 0));
        grid.TryPlace(2, Single, new Vector3Int(1, 0, 0));

        grid.Clear();

        Assert.AreEqual(0, grid.TrapCount);
        Assert.IsTrue(grid.IsFree(Single, new Vector3Int(0, 0, 0)));
    }
}