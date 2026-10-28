using System.Collections.Generic;
using System.Linq;
using QuietHome.Core.Navigation;
using Xunit;

namespace QuietHome.Core.Tests.Navigation;

/// <summary>
/// Tests of <see cref="Navigator"/>: remote control navigation on each page. The slot lists
/// are built the way the TV host builds its pages, so each test describes what the owner
/// sees: which card has focus after a key.
/// </summary>
public class NavigatorTests
{
    /// <summary>
    /// Slots of a Home page: <paramref name="inputs"/> input cards (row 0), the dock with
    /// <paramref name="dockApps"/> apps plus "Your Apps" (row 1), then the four toolbar
    /// buttons Edit inputs, Apps, Inputs, Settings (row -1).
    /// </summary>
    static SlotRows HomePage(int inputs, int dockApps = 5) =>
        new(Enumerable.Repeat(0, inputs).Concat(Enumerable.Repeat(1, dockApps + 1)).Concat(Enumerable.Repeat(-1, 4)).ToList());

    /// <summary>Slots of an Apps page with <paramref name="cards"/> app cards (rows of five), then the three toolbar buttons.</summary>
    static SlotRows AppsPage(int cards) =>
        new(Enumerable.Range(0, cards).Select(i => i / 5).Concat(Enumerable.Repeat(-1, 3)).ToList());

    /// <summary>Slots of the Settings page: four tiles in row 0, four in row 1, then the three toolbar buttons.</summary>
    static readonly SlotRows settingsPage = new(new List<int> { 0, 0, 0, 0, 1, 1, 1, 1, -1, -1, -1 });

    /// <summary>Returns a navigator showing <paramref name="page"/> with focus on slot <paramref name="selected"/> of <paramref name="zone"/>.</summary>
    static Navigator At(Page page, Zone zone, int selected = 0) => new() { Page = page, Zone = zone, Selected = selected };

    /// <summary>Home: Up goes from an input to the tabs and from the tabs to the first toolbar button; Up on the toolbar stays.</summary>
    [Fact]
    public void HomeUp()
    {
        var rows = HomePage(inputs: 3);
        var navigator = At(Page.Home, Zone.FirstRow, 2);
        Assert.Equal(KeyOutcome.Refocus, navigator.HandleKey(RemoteKey.Up, rows, 0));
        Assert.Equal(Zone.Tabs, navigator.Zone);
        navigator.HandleKey(RemoteKey.Up, rows, 0);
        Assert.Equal((Zone.Toolbar, 9), (navigator.Zone, navigator.Selected));
        navigator.HandleKey(RemoteKey.Up, rows, 0);
        Assert.Equal((Zone.Toolbar, 9), (navigator.Zone, navigator.Selected));
    }

    /// <summary>Home: Down goes from the tabs to the first input and from any input to the first dock card; Up from the dock returns to the first input.</summary>
    [Fact]
    public void HomeDownAndBack()
    {
        var rows = HomePage(inputs: 3);
        var navigator = At(Page.Home, Zone.Tabs);
        navigator.HandleKey(RemoteKey.Down, rows, 0);
        Assert.Equal((Zone.FirstRow, 0), (navigator.Zone, navigator.Selected));
        navigator.Selected = 2;
        navigator.HandleKey(RemoteKey.Down, rows, 0);
        Assert.Equal((Zone.SecondRow, 3), (navigator.Zone, navigator.Selected));
        navigator.Selected = 6;
        navigator.HandleKey(RemoteKey.Up, rows, 0);
        Assert.Equal((Zone.FirstRow, 0), (navigator.Zone, navigator.Selected));
    }

    /// <summary>Left and Right in a row of cards move to the neighbour and wrap around at both ends.</summary>
    [Fact]
    public void RowsWrap()
    {
        var rows = HomePage(inputs: 3);
        var navigator = At(Page.Home, Zone.FirstRow, 2);
        navigator.HandleKey(RemoteKey.Right, rows, 0);
        Assert.Equal(0, navigator.Selected);
        navigator.HandleKey(RemoteKey.Left, rows, 0);
        Assert.Equal(2, navigator.Selected);
        navigator = At(Page.Home, Zone.SecondRow, 3);
        navigator.HandleKey(RemoteKey.Left, rows, 0);
        Assert.Equal(8, navigator.Selected);
    }

    /// <summary>Left and Right on the tabs change the page; past either end they go to the toolbar.</summary>
    [Fact]
    public void TabsChangePages()
    {
        var navigator = At(Page.Home, Zone.Tabs, 4);
        Assert.Equal(KeyOutcome.Rebuild, navigator.HandleKey(RemoteKey.Right, HomePage(3), 0));
        Assert.Equal((Page.Apps, Zone.Tabs, 0), (navigator.Page, navigator.Zone, navigator.Selected));
        navigator = At(Page.Home, Zone.Tabs);
        Assert.Equal(KeyOutcome.Refocus, navigator.HandleKey(RemoteKey.Left, HomePage(3), 0));
        Assert.Equal((Zone.Toolbar, 12), (navigator.Zone, navigator.Selected));
        navigator = At(Page.Settings, Zone.Tabs);
        navigator.HandleKey(RemoteKey.Right, settingsPage, 0);
        Assert.Equal((Zone.Toolbar, 8), (navigator.Zone, navigator.Selected));
    }

    /// <summary>The toolbar's buttons are walked with Left and Right; past the ends focus returns to the tabs of the last or first page.</summary>
    [Fact]
    public void ToolbarEndsReturnToTheTabs()
    {
        var navigator = At(Page.Settings, Zone.Toolbar, 9);
        Assert.Equal(KeyOutcome.Refocus, navigator.HandleKey(RemoteKey.Right, settingsPage, 0));
        Assert.Equal(10, navigator.Selected);
        Assert.Equal(KeyOutcome.Rebuild, navigator.HandleKey(RemoteKey.Right, settingsPage, 0));
        Assert.Equal((Page.Home, Zone.Tabs, 0), (navigator.Page, navigator.Zone, navigator.Selected));
        navigator = At(Page.Home, Zone.Toolbar, 9);
        Assert.Equal(KeyOutcome.Rebuild, navigator.HandleKey(RemoteKey.Left, HomePage(3), 0));
        Assert.Equal((Page.Settings, Zone.Tabs), (navigator.Page, navigator.Zone));
    }

    /// <summary>Select on a tab enters the page's first card; Select on a card performs its action.</summary>
    [Fact]
    public void Select()
    {
        var navigator = At(Page.Home, Zone.Tabs, 5);
        Assert.Equal(KeyOutcome.Refocus, navigator.HandleKey(RemoteKey.Select, HomePage(3), 0));
        Assert.Equal((Zone.FirstRow, 0), (navigator.Zone, navigator.Selected));
        Assert.Equal(KeyOutcome.Activate, navigator.HandleKey(RemoteKey.Select, HomePage(3), 0));
    }

    /// <summary>The Home and settings keys work everywhere, also while adjusting; Back and Exit leave for the input outside the Apps page.</summary>
    [Fact]
    public void GlobalKeys()
    {
        var navigator = At(Page.Settings, Zone.FirstRow);
        navigator.Adjusting = Adjustment.Volume;
        Assert.Equal(KeyOutcome.GoHome, navigator.HandleKey(RemoteKey.Home, settingsPage, 0));
        Assert.Equal(KeyOutcome.OpenSettings, navigator.HandleKey(RemoteKey.Settings, settingsPage, 0));
        navigator.Adjusting = Adjustment.None;
        Assert.Equal(KeyOutcome.ReturnToInput, navigator.HandleKey(RemoteKey.Back, settingsPage, 0));
        Assert.Equal(KeyOutcome.ReturnToInput, navigator.HandleKey(RemoteKey.Exit, settingsPage, 0));
        Assert.Equal(KeyOutcome.Ignored, navigator.HandleKey(RemoteKey.Other, settingsPage, 0));
    }

    /// <summary>On Settings, Up and Down keep the column between the two rows of tiles.</summary>
    [Fact]
    public void SettingsKeepsTheColumn()
    {
        var navigator = At(Page.Settings, Zone.FirstRow, 2);
        navigator.HandleKey(RemoteKey.Down, settingsPage, 0);
        Assert.Equal((Zone.SecondRow, 6), (navigator.Zone, navigator.Selected));
        navigator.Selected = 7;
        navigator.HandleKey(RemoteKey.Up, settingsPage, 0);
        Assert.Equal((Zone.FirstRow, 3), (navigator.Zone, navigator.Selected));
    }

    /// <summary>A Home page without inputs puts focus on the dock after drawing; Up from the dock goes to the tabs and Down comes back.</summary>
    [Fact]
    public void HomeWithoutInputs()
    {
        var rows = HomePage(inputs: 0);
        var navigator = At(Page.Home, Zone.FirstRow, 3);
        navigator.SettleAfterBuild(rows);
        Assert.Equal((Zone.SecondRow, 0), (navigator.Zone, navigator.Selected));
        navigator.HandleKey(RemoteKey.Up, rows, 0);
        Assert.Equal(Zone.Tabs, navigator.Zone);
        navigator.HandleKey(RemoteKey.Down, rows, 0);
        Assert.Equal((Zone.SecondRow, 0), (navigator.Zone, navigator.Selected));
    }

    /// <summary>After drawing, a focus position past the last slot is pulled back to the last slot.</summary>
    [Fact]
    public void SettleKeepsFocusOnThePage()
    {
        var navigator = At(Page.Settings, Zone.FirstRow, 40);
        navigator.SettleAfterBuild(settingsPage);
        Assert.Equal(10, navigator.Selected);
    }

    /// <summary>
    /// Apps: Down from the tabs enters the grid, Down from the bottom row opens the next
    /// Apps page, Up from the top row of the first page returns to the tabs, Back returns Home.
    /// </summary>
    [Fact]
    public void AppsGrid()
    {
        var rows = AppsPage(15);
        var navigator = At(Page.Apps, Zone.Tabs);
        Assert.Equal(KeyOutcome.Refocus, navigator.HandleKey(RemoteKey.Down, rows, 20));
        Assert.Equal((Zone.FirstRow, 0), (navigator.Zone, navigator.Selected));
        navigator.Selected = 12;
        Assert.Equal(KeyOutcome.Rebuild, navigator.HandleKey(RemoteKey.Down, rows, 20));
        Assert.Equal((1, 2), (navigator.LibraryPage, navigator.Selected));
        navigator = At(Page.Apps, Zone.FirstRow, 3);
        Assert.Equal(KeyOutcome.Refocus, navigator.HandleKey(RemoteKey.Up, rows, 20));
        Assert.Equal(Zone.Tabs, navigator.Zone);
        Assert.Equal(KeyOutcome.LeaveApps, navigator.HandleKey(RemoteKey.Back, rows, 20));
        Assert.Equal(KeyOutcome.LeaveApps, navigator.HandleKey(RemoteKey.Exit, rows, 20));
    }

    /// <summary>Apps: Up from the tabs still reaches the toolbar, and Down from the toolbar enters the grid.</summary>
    [Fact]
    public void AppsToolbar()
    {
        var rows = AppsPage(7);
        var navigator = At(Page.Apps, Zone.Tabs);
        navigator.HandleKey(RemoteKey.Up, rows, 7);
        Assert.Equal((Zone.Toolbar, 7), (navigator.Zone, navigator.Selected));
        navigator.HandleKey(RemoteKey.Down, rows, 7);
        Assert.Equal((Zone.FirstRow, 0), (navigator.Zone, navigator.Selected));
    }

    /// <summary>While adjusting, Left and Right change the setting, other keys do nothing, and Select or Back finish.</summary>
    [Theory]
    [InlineData(RemoteKey.Select)]
    [InlineData(RemoteKey.Back)]
    public void Adjusting(RemoteKey finish)
    {
        var navigator = At(Page.Settings, Zone.FirstRow, 1);
        navigator.Adjusting = Adjustment.Brightness;
        Assert.Equal(KeyOutcome.AdjustDown, navigator.HandleKey(RemoteKey.Left, settingsPage, 0));
        Assert.Equal(KeyOutcome.AdjustUp, navigator.HandleKey(RemoteKey.Right, settingsPage, 0));
        Assert.Equal(KeyOutcome.Ignored, navigator.HandleKey(RemoteKey.Down, settingsPage, 0));
        Assert.Equal((Zone.FirstRow, 1), (navigator.Zone, navigator.Selected));
        Assert.Equal(KeyOutcome.Refocus, navigator.HandleKey(finish, settingsPage, 0));
        Assert.Equal(Adjustment.None, navigator.Adjusting);
    }

    /// <summary>The adjustment's name is what the hint and the diagnostics file show.</summary>
    [Fact]
    public void AdjustmentNames()
    {
        Assert.Equal(("", "volume", "brightness"), (Adjustment.None.Name(), Adjustment.Volume.Name(), Adjustment.Brightness.Name()));
    }
}
