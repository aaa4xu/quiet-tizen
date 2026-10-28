using System.Collections.Generic;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace QuietHome.TizenHost.Views;

/// <summary>
/// The views of the page on screen, added straight to the window in drawing order (later
/// ones on top). <see cref="HomeView"/> clears it before every rebuild; the page views add
/// to it through the helpers here. Artwork images are not part of it (see
/// <see cref="ArtworkCache"/>).
/// </summary>
sealed class Scene
{
    readonly List<View> views = new();

    /// <summary>Adds <paramref name="view"/> on top of the window and returns it.</summary>
    public T Add<T>(T view) where T : View
    {
        views.Add(view);
        Window.Default.Add(view);
        return view;
    }

    /// <summary>Adds a rounded rectangle of <paramref name="color"/> at <paramref name="x"/>, <paramref name="y"/> and returns it.</summary>
    public View Panel(int x, int y, int w, int h, Color color, int radius = 18) =>
        Add(new View { Position2D = new Position2D(x, y), Size2D = new Size2D(w, h), BackgroundColor = color, CornerRadius = new Vector4(radius, radius, radius, radius) });

    /// <summary>Adds a rounded rectangle centred on (<paramref name="centerX"/>, <paramref name="centerY"/>), which scales around its centre, and returns it.</summary>
    public View Card(int centerX, int centerY, int w, int h, Color color, int radius)
    {
        var card = Panel(centerX, centerY, w, h, color, radius);
        card.PivotPoint = new Position(.5f, .5f, .5f);
        card.PositionUsesPivotPoint = true;
        return card;
    }

    /// <summary>Adds a one-line text in <paramref name="box"/> and returns it.</summary>
    public TextLabel Text(string text, Box box, float size, Color color, string face = "Medium") =>
        Add(Label(text, box, size, color, face));

    /// <summary>Adds a right-aligned text in <paramref name="box"/> and returns it.</summary>
    public TextLabel TextEnd(string text, Box box, float size, Color color)
    {
        var label = Text(text, box, size, color);
        label.HorizontalAlignment = HorizontalAlignment.End;
        return label;
    }

    /// <summary>Returns a text in <paramref name="box"/>, vertically centred, in Quiet Inter <paramref name="face"/>, not yet added anywhere.</summary>
    public static TextLabel Label(string text, Box box, float size, Color color, string face = "Medium") =>
        new()
        {
            Text = text,
            Position2D = new Position2D(box.X, box.Y),
            Size2D = new Size2D(box.W, box.H),
            PixelSize = size,
            FontFamily = Font(face),
            TextColor = color,
            VerticalAlignment = VerticalAlignment.Center,
        };

    /// <summary>Returns the font family name of Quiet Inter <paramref name="face"/> ("Medium", "Semibold" or "Display").</summary>
    public static string Font(string face) => "Quiet Inter " + face;

    /// <summary>Removes and destroys every view of the page.</summary>
    public void Clear()
    {
        foreach (var view in views)
        {
            Window.Default.Remove(view);
            view.Dispose();
        }
        views.Clear();
    }
}
