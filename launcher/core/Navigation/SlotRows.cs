using System.Collections.Generic;

namespace QuietHome.Core.Navigation;

/// <summary>
/// The focusable places ("slots") on the current page and the row each one is in, in the
/// order the page created them. <see cref="Navigator"/> moves focus between slots by their
/// position in this list, so it needs the rows but not the drawing. Rows: -1 is the
/// toolbar, 0 is the first row of cards (on the Apps page 0, 1, 2 are the grid rows),
/// 1 is the second row.
/// </summary>
public sealed class SlotRows
{
    readonly IReadOnlyList<int> rows;

    /// <summary>Wraps the row of each slot, in slot order.</summary>
    public SlotRows(IReadOnlyList<int> rows) => this.rows = rows;

    /// <summary>Number of slots on the page.</summary>
    public int Count => rows.Count;

    /// <summary>Returns the positions (slot indexes) of all slots in <paramref name="row"/>, left to right.</summary>
    public List<int> Row(int row)
    {
        var result = new List<int>();
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] == row)
                result.Add(i);
        }
        return result;
    }
}
