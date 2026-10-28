using QuietHome.Core.Screens;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace QuietHome.TizenHost.Views;

/// <summary>
/// Draws the Settings page from a <see cref="SettingsPageModel"/>: eight colored tiles in a
/// 4 × 2 grid. The values are filled in when the focus is applied, because they change
/// without a rebuild. <see cref="HomeView"/> calls it during a rebuild.
/// </summary>
static class SettingsPageView
{
    /// <summary>Adds one tile per model tile, with its focus ring, and a slot that holds the tile's value line.</summary>
    public static void Build(PageCanvas canvas, SettingsPageModel page)
    {
        foreach (var model in page.Tiles)
        {
            var tile = model.Tile;
            int w = Layout.TileWidth, h = Layout.TileHeight;
            int x = Layout.Margin + tile.Column * Layout.TileColumnStep, y = Layout.TileTop + tile.Row * Layout.TileRowStep;
            var card = canvas.Scene.Card(x + w / 2, y + h / 2, w, h, ViewPalette.Clear(), 24);
            card.Add(new ImageView { ResourceUrl = canvas.Root + "quick-" + tile.Icon + ".svg", Size2D = new Size2D(w, h) });
            card.Add(Scene.Label(tile.Title, Layout.TileTitle, Layout.TileTitleSize, ViewPalette.White(), "Semibold"));
            var value = Scene.Label("", Layout.TileValue, Layout.TileValueSize, ViewPalette.White(), "Display");
            card.Add(value);
            card.Add(Scene.Label(tile.Hint, Layout.TileHint, Layout.TileHintSize, ViewPalette.TileHint()));
            var ring = FocusStyle.AddRing(card, w, h, 24, canvas.Palette.Ink);
            canvas.Slots.Add(new SlotView(card, new Box(x, y, w, h)) { Ring = ring, Value = value });
        }
    }
}
