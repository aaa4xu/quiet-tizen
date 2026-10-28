using Tizen.NUI.BaseComponents;

namespace QuietHome.TizenHost.Views;

/// <summary>
/// The views of one focusable element as drawn: the card itself and the parts the focus
/// changes. The page views create one per card of the page model, in the same order;
/// <see cref="HomeView"/> changes them when the focus moves (see <see cref="FocusStyle"/>).
/// </summary>
sealed class SlotView
{
    /// <summary>Creates the slot for <paramref name="card"/>, which is drawn in <paramref name="box"/> when not focused.</summary>
    public SlotView(View card, Box box)
    {
        Card = card;
        Box = box;
    }

    /// <summary>The card; it scales and moves around its centre.</summary>
    public View Card { get; }

    /// <summary>Where the card is when it is not focused.</summary>
    public Box Box { get; }

    /// <summary>The shadow under the card, brighter while focused; null when the card has none.</summary>
    public View? Shadow { get; init; }

    /// <summary>The accent ring shown on a focused app card; null for other cards.</summary>
    public View? FocusRing { get; init; }

    /// <summary>
    /// The card's text that changes with focus: the name under input and app cards (bolder
    /// while focused) or the value on a Settings tile. Null when there is none.
    /// </summary>
    public TextLabel? Label { get; init; }
}
