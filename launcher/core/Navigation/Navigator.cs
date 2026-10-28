using QuietHome.Core.Catalog;

namespace QuietHome.Core.Navigation;

/// <summary>
/// Remote control navigation for all three pages: which page is shown, which zone and
/// slot have focus, which Apps page is open and whether a setting is being adjusted.
/// <see cref="HomeController"/> owns one navigator, passes every accepted key to
/// <see cref="HandleKey"/> together with the current page's <see cref="SlotRows"/>, and then
/// does what the returned <see cref="KeyOutcome"/> says. The controller also sets the state
/// directly when the TV asks for a page (Home key, launch options).
/// </summary>
public sealed class Navigator
{
    /// <summary>The page on screen.</summary>
    public Page Page { get; set; }

    /// <summary>The zone with focus.</summary>
    public Zone Zone { get; set; } = Zone.FirstRow;

    /// <summary>
    /// Position of the focused slot in the page's slot list (see <see cref="SlotRows"/>).
    /// On the Apps page the app cards come first, so it is also the card's index on the
    /// current Apps page.
    /// </summary>
    public int Selected { get; set; }

    /// <summary>The open Apps page, 0-based.</summary>
    public int LibraryPage { get; set; }

    /// <summary>The setting Left and Right adjust, or <see cref="Adjustment.None"/>.</summary>
    public Adjustment Adjusting { get; set; }

    /// <summary>
    /// Updates the focus for one key press and returns what the controller must do next.
    /// <paramref name="rows"/> describes the page on screen; <paramref name="catalogCount"/>
    /// is the number of apps in the catalog, for the Apps page grid.
    /// </summary>
    public KeyOutcome HandleKey(RemoteKey key, SlotRows rows, int catalogCount)
    {
        if (key == RemoteKey.Home)
            return KeyOutcome.GoHome;
        if (key == RemoteKey.Settings)
            return KeyOutcome.OpenSettings;
        if (Page == Page.Apps && HandleAppsKey(key, catalogCount, out var appsOutcome))
            return appsOutcome;
        if (Adjusting != Adjustment.None)
            return HandleAdjustKey(key);
        switch (key)
        {
            case RemoteKey.Up:
                MoveUp(rows);
                return KeyOutcome.Refocus;
            case RemoteKey.Down:
                MoveDown(rows);
                return KeyOutcome.Refocus;
            case RemoteKey.Left:
                return MoveSideways(rows, -1);
            case RemoteKey.Right:
                return MoveSideways(rows, 1);
            case RemoteKey.Select:
                if (Zone != Zone.Tabs)
                    return KeyOutcome.Activate;
                // Select on a tab enters the page.
                Zone = Zone.FirstRow;
                Selected = 0;
                LeaveEmptyRow(rows);
                return KeyOutcome.Refocus;
            case RemoteKey.Back:
            case RemoteKey.Exit:
                return KeyOutcome.ReturnToInput;
            default:
                return KeyOutcome.Ignored;
        }
    }

    /// <summary>
    /// Fixes the focus for a page about to be drawn: keeps <see cref="Selected"/> within
    /// the page's slots and moves focus off a row that has no cards (see <see cref="LeaveEmptyRow"/>).
    /// </summary>
    public void SettleAfterBuild(SlotRows rows)
    {
        Selected = System.Math.Max(0, System.Math.Min(Selected, rows.Count - 1));
        LeaveEmptyRow(rows);
    }

    /// <summary>
    /// Moves focus off a row of cards that has no cards: on a Home page without inputs to the
    /// first dock card, elsewhere (the Apps page of an empty catalog) to the tabs. Does nothing
    /// when the focused row has cards or focus is not on a row of cards.
    /// </summary>
    void LeaveEmptyRow(SlotRows rows)
    {
        if ((Zone != Zone.FirstRow && Zone != Zone.SecondRow) || rows.Row((int)Zone - 1).Count > 0)
            return;
        var dock = rows.Row(1);
        if (Page == Page.Home && Zone == Zone.FirstRow && dock.Count > 0)
        {
            Zone = Zone.SecondRow;
            Selected = dock[0];
        }
        else
        {
            Zone = Zone.Tabs;
        }
    }

    /// <summary>
    /// Handles the keys the Apps page treats specially: Back and Exit return to Home, and the
    /// direction keys move through the app grid, across Apps pages. Returns false when the
    /// key should get the normal handling (for example Up from the tabs to the toolbar).
    /// </summary>
    bool HandleAppsKey(RemoteKey key, int catalogCount, out KeyOutcome outcome)
    {
        outcome = KeyOutcome.Ignored;
        if (key == RemoteKey.Back || key == RemoteKey.Exit)
        {
            outcome = KeyOutcome.LeaveApps;
            return true;
        }
        // Without apps Up and Down have nothing to reach.
        if (catalogCount == 0)
            return key == RemoteKey.Up || key == RemoteKey.Down;
        if ((Zone == Zone.Tabs || Zone == Zone.Toolbar) && key == RemoteKey.Down)
        {
            Zone = Zone.FirstRow;
            Selected = 0;
            outcome = KeyOutcome.Refocus;
            return true;
        }
        if (Zone != Zone.FirstRow || !TryDirection(key, out var direction))
            return false;
        int next = CatalogGrid.Move(catalogCount, LibraryPage * CatalogGrid.PageSize + Selected, direction);
        if (next < 0)
        {
            Zone = Zone.Tabs;
            outcome = KeyOutcome.Refocus;
            return true;
        }
        int nextPage = next / CatalogGrid.PageSize;
        Selected = next % CatalogGrid.PageSize;
        if (nextPage != LibraryPage)
        {
            LibraryPage = nextPage;
            outcome = KeyOutcome.Rebuild;
        }
        else
        {
            outcome = KeyOutcome.Refocus;
        }
        return true;
    }

    /// <summary>
    /// Handles a key while a setting is being adjusted: Left and Right change it, Select,
    /// Back or Exit finish the adjustment, other keys do nothing.
    /// </summary>
    KeyOutcome HandleAdjustKey(RemoteKey key)
    {
        switch (key)
        {
            case RemoteKey.Left:
                return KeyOutcome.AdjustDown;
            case RemoteKey.Right:
                return KeyOutcome.AdjustUp;
            case RemoteKey.Select:
            case RemoteKey.Back:
            case RemoteKey.Exit:
                Adjusting = Adjustment.None;
                return KeyOutcome.Refocus;
            default:
                return KeyOutcome.Ignored;
        }
    }

    /// <summary>
    /// Moves focus one zone up: second row → first row (same column on Settings, else the
    /// first card), first row → tabs, tabs → toolbar. On a page whose first row is empty,
    /// the second row goes straight to the tabs.
    /// </summary>
    void MoveUp(SlotRows rows)
    {
        if (Zone == Zone.SecondRow && rows.Row(0).Count == 0)
        {
            Zone = Zone.Tabs;
        }
        else if (Zone == Zone.SecondRow)
        {
            int column = rows.Row(1).IndexOf(Selected);
            Zone = Zone.FirstRow;
            Selected = rows.Row(0)[Page == Page.Settings ? column : 0];
        }
        else if (Zone == Zone.FirstRow)
        {
            Zone = Zone.Tabs;
        }
        else if (Zone == Zone.Tabs)
        {
            Zone = Zone.Toolbar;
            Selected = rows.Row(-1)[0];
        }
    }

    /// <summary>
    /// Moves focus one zone down: toolbar or tabs → first row (or the second row when the
    /// first is empty), first row → second row (same column on Settings, else the first card).
    /// </summary>
    void MoveDown(SlotRows rows)
    {
        if ((Zone == Zone.Tabs || Zone == Zone.Toolbar) && rows.Row(0).Count == 0)
        {
            Zone = Zone.SecondRow;
            Selected = rows.Row(1)[0];
        }
        else if (Zone == Zone.Tabs || Zone == Zone.Toolbar)
        {
            Zone = Zone.FirstRow;
            Selected = rows.Row(0)[0];
        }
        else if (Zone == Zone.FirstRow)
        {
            int column = rows.Row(0).IndexOf(Selected);
            Zone = Zone.SecondRow;
            Selected = rows.Row(1)[Page == Page.Settings ? column : 0];
        }
    }

    /// <summary>
    /// Moves focus left (<paramref name="step"/> = -1) or right (+1). On the tabs it changes
    /// the page; past the first or last tab it goes to the toolbar. On the toolbar it moves
    /// between buttons and past either end goes back to the tabs (the last or first page).
    /// In a row of cards it moves to the neighbour, wrapping around at the ends; in an empty
    /// row it leaves the row (see <see cref="LeaveEmptyRow"/>).
    /// </summary>
    KeyOutcome MoveSideways(SlotRows rows, int step)
    {
        if (Zone == Zone.Tabs)
        {
            int next = (int)Page + step;
            if (next < 0 || next > 2)
            {
                Zone = Zone.Toolbar;
                var toolbar = rows.Row(-1);
                Selected = toolbar[next < 0 ? toolbar.Count - 1 : 0];
                return KeyOutcome.Refocus;
            }
            Page = (Page)next;
            Selected = 0;
            return KeyOutcome.Rebuild;
        }
        if (Zone == Zone.Toolbar)
        {
            var toolbar = rows.Row(-1);
            int n = toolbar.IndexOf(Selected) + step;
            if (n < 0 || n >= toolbar.Count)
            {
                Zone = Zone.Tabs;
                Page = n < 0 ? Page.Settings : Page.Home;
                Selected = 0;
                return KeyOutcome.Rebuild;
            }
            Selected = toolbar[n];
            return KeyOutcome.Refocus;
        }
        var row = rows.Row((int)Zone - 1);
        if (row.Count == 0)
        {
            LeaveEmptyRow(rows);
            return KeyOutcome.Refocus;
        }
        int index = row.IndexOf(Selected);
        Selected = row[(index + step + row.Count) % row.Count];
        return KeyOutcome.Refocus;
    }

    /// <summary>Returns the grid direction for a direction key; false for any other key.</summary>
    static bool TryDirection(RemoteKey key, out Direction direction)
    {
        direction = key switch
        {
            RemoteKey.Left => Direction.Left,
            RemoteKey.Right => Direction.Right,
            RemoteKey.Up => Direction.Up,
            _ => Direction.Down,
        };
        return key == RemoteKey.Left || key == RemoteKey.Right || key == RemoteKey.Up || key == RemoteKey.Down;
    }
}
