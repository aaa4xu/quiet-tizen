using System;
using QuietHome.Core.Navigation;

namespace QuietHome.Core.Catalog;

/// <summary>
/// The Apps page arithmetic: how the catalog splits into pages of <see cref="Columns"/> ×
/// <see cref="Rows"/> cards and where focus goes when a direction key is pressed. It knows
/// nothing about drawing; <see cref="Navigator"/> and the TV host ask it for numbers
/// (catalog indexes, page counts) and draw the cards themselves.
/// </summary>
public static class CatalogGrid
{
    /// <summary>Apps page grid: cards per row, rows per page and cards per page.</summary>
    public const int Columns = 5, Rows = 3, PageSize = Columns * Rows;

    /// <summary>Number of Apps pages needed for <paramref name="count"/> apps (0 for an empty catalog).</summary>
    public static int PageCount(int count) => (count + PageSize - 1) / PageSize;

    /// <summary>Number of cards on page <paramref name="page"/> (0-based): a full page, the remainder on the last page, or 0 past the end.</summary>
    public static int VisibleCount(int count, int page) => Math.Max(0, Math.Min(PageSize, count - page * PageSize));

    /// <summary>
    /// Works out where focus goes on the Apps page when a direction key is pressed.
    /// <paramref name="absolute"/> is the focused card's index in the whole catalog. Returns
    /// the new catalog index, which may be on the previous or next page, or -1 when focus
    /// leaves the grid for the navigation tabs (Up from the first row of the first page, or
    /// an empty catalog).
    /// </summary>
    public static int Move(int count, int absolute, Direction direction)
    {
        if (count <= 0)
            return -1;
        absolute = Math.Clamp(absolute, 0, count - 1);
        int page = absolute / PageSize, local = absolute % PageSize, col = local % Columns, row = local / Columns;
        int pageStart = page * PageSize, visible = VisibleCount(count, page);
        switch (direction)
        {
            case Direction.Left:
                return Math.Max(0, absolute - 1);
            case Direction.Right:
                return Math.Min(count - 1, absolute + 1);
            case Direction.Up:
                // Up from the top row goes to the same column on the last row of the previous page.
                if (row > 0)
                    return absolute - Columns;
                if (page == 0)
                    return -1;
                return pageStart - Columns + col;
            default:
                // Down moves one row down; when the row below is shorter, focus lands on its last card.
                // From the bottom row it goes to the same column on the next page (or that page's
                // last card); on the last page it stays put.
                if ((row + 1) * Columns < visible)
                    return pageStart + Math.Min(local + Columns, visible - 1);
                int next = pageStart + PageSize;
                return next < count ? Math.Min(next + col, count - 1) : absolute;
        }
    }
}
