using QuietHome.Core.Screens;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace QuietHome.TizenHost.Views;

/// <summary>
/// Draws the Apps page from an <see cref="AppsPageModel"/>: one page of the app grid with
/// the counters above it. <see cref="HomeView"/> calls it during a rebuild.
/// </summary>
static class AppsPageView
{
    /// <summary>Adds the page's views to the canvas and one slot per app card, or the load error when the catalog is empty.</summary>
    public static void Build(PageCanvas canvas, AppsPageModel page)
    {
        var scene = canvas.Scene;
        var palette = canvas.Palette;
        if (page.LoadFailed)
        {
            scene.Text(AppsPageModel.LoadFailedText, Layout.AppsLoadFailed, Layout.AppsLoadFailedSize, palette.Ink);
            return;
        }
        foreach (var appCard in page.Apps)
            BuildCard(canvas, appCard);
        scene.Text(page.CountText, Layout.AppCount, Layout.AppCounterSize, palette.Muted);
        if (page.PagesText != null)
            scene.TextEnd(page.PagesText, Layout.AppPages, Layout.AppCounterSize, palette.Muted);
    }

    /// <summary>Adds one app card with its shadow, artwork, focus ring and name.</summary>
    static void BuildCard(PageCanvas canvas, AppCardModel appCard)
    {
        var scene = canvas.Scene;
        var palette = canvas.Palette;
        var app = appCard.App;
        int column = appCard.Index % 5, row = appCard.Index / 5;
        int x = Layout.Margin + column * Layout.AppColumnStep, y = Layout.AppTop + row * Layout.AppRowStep;
        int w = Layout.AppWidth, h = Layout.AppHeight;
        var shadow = scene.Panel(x - 5, y + 7, w + 10, h + 8, ViewPalette.AppShadow(), 22);
        var color = app.Utility && palette.Dark ? ViewPalette.DarkUtilityCard() : ViewPalette.FromHex(app.Background);
        var card = scene.Card(x + w / 2, y + h / 2, w, h, color, 18);
        if (AppArtwork.Fill(card, app, w, h, canvas.Artwork, palette))
            canvas.ColdArtwork++;
        var ring = new View
        {
            Size2D = new Size2D(w, h),
            BackgroundColor = ViewPalette.Clear(),
            CornerRadius = new Vector4(18, 18, 18, 18),
            BorderlineWidth = 4,
            BorderlineColor = palette.Accent,
            Opacity = 0,
        };
        card.Add(ring);
        var name = scene.Text(app.Name, new Box(x, y + h + Layout.AppNameGap, w, Layout.AppNameHeight), Layout.AppNameSize, palette.Ink, "Semibold");
        canvas.Slots.Add(new SlotView(card, new Box(x, y, w, h)) { Shadow = shadow, FocusRing = ring, Label = name });
    }
}
