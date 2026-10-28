using System.Collections.Generic;
using QuietHome.Core.Screens;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace QuietHome.TizenHost.Views;

/// <summary>
/// Draws the Home page from a <see cref="HomePageModel"/>: the input cards on top, then the
/// glass shelf and the dock. <see cref="HomeView"/> calls it during a rebuild, and again
/// whenever the focus moves, to widen the focused input card (<see cref="Place"/>).
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

    /// <summary>
    /// Adds the input cards side by side, all equally wide, or "No connected inputs found."
    /// when there are none. <see cref="Place"/> then widens the focused one.
    /// </summary>
    static void BuildInputs(PageCanvas canvas, HomePageModel page)
    {
        var scene = canvas.Scene;
        var palette = canvas.Palette;
        if (page.Inputs.Count == 0)
        {
            scene.Text(HomePageModel.NoInputsText, Layout.NoInputs, Layout.NoInputsSize, palette.Ink, "Semibold");
            return;
        }
        foreach (var input in page.Inputs)
        {
            var box = Layout.InputBox(input.Index, page.Inputs.Count, -1);
            int x = box.X, y = box.Y, w = box.W, h = box.H;
            var shadow = scene.Add(new ImageView
            {
                ResourceUrl = canvas.Root + "shadow-portrait.svg",
                Position2D = new Position2D(x - 24, y - 10),
                Size2D = new Size2D(w + 48, h + 64),
            });
            var card = scene.Card(x + w / 2, y + h / 2, w, h, palette.InputCard, Layout.CardRadius);
            if (!string.IsNullOrWhiteSpace(input.IconPath))
            {
                int icon = Layout.InputIconSize;
                // Centred on the card, so it stays in the middle while the card widens.
                card.Add(new ImageView
                {
                    ResourceUrl = input.IconPath,
                    ParentOrigin = ParentOrigin.Center,
                    PivotPoint = PivotPoint.Center,
                    PositionUsesPivotPoint = true,
                    Size2D = new Size2D(icon, icon),
                    FittingMode = FittingModeType.ShrinkToFit,
                });
            }
            var ring = FocusStyle.AddRing(card, w, h, Layout.CardRadius, palette.Ink);
            var title = scene.Text(input.Name, new Box(x, Layout.InputNameY, w, Layout.InputNameHeight), Layout.InputNameSize, palette.Ink);
            var subtitle = scene.Text(input.Text, new Box(x, Layout.InputTextY, w, Layout.InputTextHeight), Layout.InputTextSize, palette.Muted);
            canvas.Slots.Add(new SlotView(card, box) { Shadow = shadow, Ring = ring, Title = title, Subtitle = subtitle });
        }
    }

    /// <summary>
    /// Moves the input cards of <paramref name="page"/> (the first slots of
    /// <paramref name="slots"/>) to where the focus puts them: the expanded card (see
    /// <see cref="InputCardModel.Expanded"/>) 16:9 wide and the others narrower, or all equal
    /// when none is expanded. Each card takes its shadow,
    /// ring and texts along, through <paramref name="animation"/> when there is one.
    /// </summary>
    public static void Place(IReadOnlyList<SlotView> slots, HomePageModel page, Animation? animation)
    {
        int count = page.Inputs.Count;
        int focused = -1;
        for (int i = 0; i < count; i++)
        {
            if (page.Inputs[i].Expanded)
                focused = i;
        }
        for (int i = 0; i < count && i < slots.Count; i++)
        {
            var box = Layout.InputBox(i, count, focused);
            if (box != slots[i].Box)
                Move(slots[i], box, animation);
        }
    }

    /// <summary>Moves one input card with its shadow, ring and texts to <paramref name="box"/>.</summary>
    static void Move(SlotView slot, Box box, Animation? animation)
    {
        slot.Box = box;
        To(slot.Card, "Position", new Position(box.X + box.W / 2f, box.Y + box.H / 2f), animation);
        To(slot.Card, "Size", new Size(box.W, box.H), animation);
        if (slot.Shadow != null)
        {
            To(slot.Shadow, "Position", new Position(box.X - 24, box.Y - 10), animation);
            To(slot.Shadow, "Size", new Size(box.W + 48, box.H + 64), animation);
        }
        if (slot.Ring != null)
            To(slot.Ring, "Size", FocusStyle.RingSize(box.W, box.H), animation);
        foreach (var label in new[] { slot.Title, slot.Subtitle })
        {
            if (label == null)
                continue;
            // Text is left-aligned, so only its start moves; the width changes at once.
            label.Size2D = new Size2D(box.W, label.Size2D.Height);
            To(label, "Position", new Position(box.X, label.Position.Y), animation);
        }
    }

    /// <summary>
    /// Sets <paramref name="property"/> of <paramref name="view"/> to <paramref name="value"/>,
    /// a <see cref="Position"/> for "Position" or a <see cref="Size"/> for "Size", through
    /// <paramref name="animation"/> when there is one.
    /// </summary>
    static void To(View view, string property, object value, Animation? animation)
    {
        if (animation != null)
            animation.AnimateTo(view, property, value);
        else if (value is Position position)
            view.Position = position;
        else if (value is Size size)
            view.Size = size;
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
        var card = scene.Card(x + w / 2, y + h / 2, w, h, ViewPalette.Clear(), Layout.CardRadius);
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
        var ring = FocusStyle.AddRing(card, w, h, Layout.CardRadius, canvas.Palette.Ink);
        canvas.Slots.Add(new SlotView(card, new Box(x, y, w, h)) { Shadow = shadow, Ring = ring });
    }
}
