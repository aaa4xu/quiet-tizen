using QuietHome.Core.Screens;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace QuietHome.TizenHost.Views;

/// <summary>
/// Draws the Home page from a <see cref="HomePageModel"/>: the input cards on top, then the
/// glass shelf and the dock. <see cref="HomeView"/> calls it during a rebuild.
/// </summary>
static class HomePageView
{
    /// <summary>Adds the page's views to the canvas and one slot per input and dock card, in model order.</summary>
    public static void Build(PageCanvas canvas, HomePageModel page)
    {
        BuildInputs(canvas, page);
        var scene = canvas.Scene;
        var shelf = Layout.Shelf;
        scene.Add(new ImageView
        {
            ResourceUrl = canvas.Root + canvas.Palette.Themed("glass-shelf.svg"),
            Position2D = new Position2D(shelf.X, shelf.Y),
            Size2D = new Size2D(shelf.W, shelf.H),
        });
        foreach (var dock in page.Dock)
            BuildDockCard(canvas, dock);
    }

    /// <summary>Adds the input cards side by side, or "No connected inputs found." when there are none.</summary>
    static void BuildInputs(PageCanvas canvas, HomePageModel page)
    {
        var scene = canvas.Scene;
        var palette = canvas.Palette;
        if (page.Inputs.Count == 0)
        {
            scene.Text(HomePageModel.NoInputsText, Layout.NoInputs, Layout.NoInputsSize, palette.Ink, "Semibold");
            return;
        }
        int w = Layout.InputWidth(page.Inputs.Count), h = Layout.InputHeight, y = Layout.InputY;
        foreach (var input in page.Inputs)
        {
            int x = Layout.Margin + input.Index * (w + Layout.InputGap);
            var shadow = scene.Add(new ImageView
            {
                ResourceUrl = canvas.Root + "shadow-portrait.svg",
                Position2D = new Position2D(x - 24, y - 10),
                Size2D = new Size2D(w + 48, h + 64),
            });
            var card = scene.Card(x + w / 2, y + h / 2, w, h, palette.InputCard, 18);
            if (!string.IsNullOrWhiteSpace(input.IconPath))
            {
                int icon = Layout.InputIconSize;
                card.Add(new ImageView
                {
                    ResourceUrl = input.IconPath,
                    Position2D = new Position2D((w - icon) / 2, (h - icon) / 2),
                    Size2D = new Size2D(icon, icon),
                    FittingMode = FittingModeType.ShrinkToFit,
                });
            }
            var ring = FocusStyle.AddRing(card, w, h, 18, palette.Ink);
            scene.Text(input.Name, new Box(x, Layout.InputNameY, w, Layout.InputNameHeight), Layout.InputNameSize, palette.Ink);
            scene.Text(input.Text, new Box(x, Layout.InputTextY, w, Layout.InputTextHeight), Layout.InputTextSize, palette.Muted);
            canvas.Slots.Add(new SlotView(card, new Box(x, y, w, h)) { Shadow = shadow, Ring = ring });
        }
    }

    /// <summary>Adds one dock card: an app with its artwork on its card color, or the "Your Apps" card.</summary>
    static void BuildDockCard(PageCanvas canvas, DockCardModel dock)
    {
        var scene = canvas.Scene;
        int x = Layout.DockX(dock.Index), w = Layout.DockWidth, h = Layout.DockHeight, y = Layout.DockY;
        var shadow = scene.Add(new ImageView
        {
            ResourceUrl = canvas.Root + "shadow-dock.svg",
            Position2D = new Position2D(x - 24, y - 12),
            Size2D = new Size2D(w + 48, h + 64),
        });
        var card = scene.Card(x + w / 2, y + h / 2, w, h, ViewPalette.Clear(), 22);
        if (dock.App != null)
        {
            card.BackgroundColor = ViewPalette.FromHex(dock.App.Background);
            if (AppArtwork.Fill(card, dock.App, w, h, canvas.Artwork, canvas.Palette))
                canvas.ColdArtwork++;
        }
        else
        {
            card.Add(new ImageView { ResourceUrl = canvas.Root + "free-dock-apps.svg", Size2D = new Size2D(w, h) });
        }
        var ring = FocusStyle.AddRing(card, w, h, 22, canvas.Palette.Ink);
        canvas.Slots.Add(new SlotView(card, new Box(x, y, w, h)) { Shadow = shadow, Ring = ring });
    }
}
