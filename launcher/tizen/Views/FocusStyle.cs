using QuietHome.Core.Screens;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;

namespace QuietHome.TizenHost.Views;

/// <summary>
/// How a focusable element looks with and without the focus. Every focusable element (input
/// cards, app cards on the Apps page and in the dock, Settings tiles and the round toolbar
/// buttons) grows by the same number of pixels and shows a ring outside its edge, which stays
/// visible on any card color. <see cref="HomeView"/> looks the style up for every card when the
/// focus moves; the views add the ring with <see cref="AddRing"/>.
/// </summary>
/// <param name="Grow">How many pixels taller the focused card gets, half above and half below.</param>
/// <param name="Border">Width of the faint white border the card always has.</param>
readonly record struct FocusStyle(float Grow, int Border)
{
    /// <summary>Width of the focus ring.</summary>
    const int RingWidth = 4;

    /// <summary>Gap between the card's edge and the focus ring.</summary>
    const int RingGap = 4;

    /// <summary>
    /// The look of input cards, app cards and Settings tiles. The growth and the ring together
    /// reach 13 pixels past the card, which the gaps to the names around it allow.
    /// </summary>
    public static FocusStyle Card { get; } = new(10, 1);

    /// <summary>The look of the round toolbar buttons: the same growth and ring, but no border, since the glass artwork has its own edge.</summary>
    public static FocusStyle Toolbar { get; } = new(10, 0);

    /// <summary>Returns the style of <paramref name="card"/>: <see cref="Toolbar"/> for toolbar buttons, otherwise <see cref="Card"/>.</summary>
    public static FocusStyle For(CardModel card) => card is ToolbarButtonModel ? Toolbar : Card;

    /// <summary>Returns the scale of a focused card <paramref name="height"/> pixels tall.</summary>
    public float Scale(int height) => 1 + Grow / height;

    /// <summary>Returns the size of the ring around a card of <paramref name="w"/> × <paramref name="h"/>.</summary>
    public static Size RingSize(int w, int h) => new(w + 2 * (RingGap + RingWidth), h + 2 * (RingGap + RingWidth));

    /// <summary>
    /// Adds a hidden focus ring in <paramref name="color"/> around <paramref name="card"/> of
    /// <paramref name="w"/> × <paramref name="h"/> with corner radius <paramref name="radius"/>,
    /// and returns it. The ring is a child of the card, so it grows with it; a card that
    /// changes size gives its ring <see cref="RingSize"/>.
    /// </summary>
    public static View AddRing(View card, int w, int h, int radius, Color color)
    {
        int outset = RingGap + RingWidth;
        float corner = radius + outset;
        var ring = new View
        {
            Position2D = new Position2D(-outset, -outset),
            Size = RingSize(w, h),
            BackgroundColor = ViewPalette.Clear(),
            CornerRadius = new Vector4(corner, corner, corner, corner),
            BorderlineWidth = RingWidth,
            BorderlineColor = color,
            BorderlineOffset = -1,
            Opacity = 0,
        };
        card.Add(ring);
        return ring;
    }
}
