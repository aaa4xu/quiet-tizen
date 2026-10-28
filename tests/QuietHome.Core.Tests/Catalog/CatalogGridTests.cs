using QuietHome.Core.Catalog;
using QuietHome.Core.Navigation;
using Xunit;

namespace QuietHome.Core.Tests.Catalog;

/// <summary>
/// Tests of <see cref="CatalogGrid"/>: page arithmetic and focus movement on the Apps page.
/// </summary>
public class CatalogGridTests
{
    /// <summary>Focus movement at page edges and short rows: (catalog size, focused index, key) gives the expected index.</summary>
    [Theory]
    [InlineData(31, 0, Direction.Up, -1)] // top row returns to tabs
    [InlineData(31, 10, Direction.Down, 15)] // last row enters next page
    [InlineData(31, 30, Direction.Up, 25)] // partial page returns to matching previous column
    [InlineData(31, 29, Direction.Down, 30)] // last partial page clamps column
    [InlineData(12, 9, Direction.Down, 11)] // partial bottom row clamps column
    [InlineData(12, 11, Direction.Right, 11)] // last item remains selected
    [InlineData(0, 0, Direction.Down, -1)] // empty catalog
    public void Move(int count, int absolute, Direction direction, int expected)
    {
        Assert.Equal(expected, CatalogGrid.Move(count, absolute, direction));
    }

    /// <summary>For every catalog size from 1 to 47 apps (four pages), no key moves focus outside the catalog except to the tabs (-1).</summary>
    [Fact]
    public void MoveStaysInsideTheCatalogOrGoesToTheTabs()
    {
        for (int count = 1; count < 48; count++)
        {
            for (int item = 0; item < count; item++)
            {
                foreach (var direction in new[] { Direction.Left, Direction.Right, Direction.Up, Direction.Down })
                {
                    int next = CatalogGrid.Move(count, item, direction);
                    Assert.InRange(next, -1, count - 1);
                }
            }
        }
    }

    /// <summary>Pages hold fifteen cards; the last page holds the rest; an empty catalog has no pages.</summary>
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(15, 1, 15)]
    [InlineData(16, 2, 15)]
    [InlineData(31, 3, 15)]
    public void PageCounts(int count, int pages, int firstPage)
    {
        Assert.Equal(pages, CatalogGrid.PageCount(count));
        Assert.Equal(firstPage, CatalogGrid.VisibleCount(count, 0));
        Assert.Equal(0, CatalogGrid.VisibleCount(count, pages));
    }
}
