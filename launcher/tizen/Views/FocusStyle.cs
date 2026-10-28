using QuietHome.Core.Screens;

namespace QuietHome.TizenHost.Views;

/// <summary>
/// How each kind of card looks with and without the focus: how much it grows, its border,
/// whether it lifts and whether its name turns bold. <see cref="HomeView"/> looks the style
/// up for every card when the focus moves.
/// </summary>
/// <param name="Scale">Size of the focused card relative to the normal one.</param>
/// <param name="Border">Border width without focus.</param>
/// <param name="FocusedBorder">Border width with focus.</param>
/// <param name="AccentBorder">True when the focused border is in the accent color instead of white.</param>
/// <param name="Lift">How many pixels the focused card moves up.</param>
/// <param name="BoldName">True when the name under the card turns semibold with focus.</param>
readonly record struct FocusStyle(float Scale, int Border, int FocusedBorder, bool AccentBorder, int Lift, bool BoldName)
{
    /// <summary>Returns the style of <paramref name="card"/>'s kind; the top row of Settings tiles differs from the bottom one.</summary>
    public static FocusStyle For(CardModel card) => card switch
    {
        InputCardModel => new FocusStyle(1.05f, 1, 1, false, 5, true),
        DockCardModel => new FocusStyle(1.20f, 0, 0, false, 0, false),
        AppCardModel => new FocusStyle(1.055f, 1, 4, true, 5, true),
        SettingsTileModel tile when tile.Row == 0 => new FocusStyle(1.035f, 1, 1, false, 5, false),
        SettingsTileModel => new FocusStyle(1.035f, 0, 0, false, 0, false),
        _ => new FocusStyle(1.10f, 0, 0, false, 0, false),
    };
}
