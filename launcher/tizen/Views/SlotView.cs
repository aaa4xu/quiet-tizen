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

    /// <summary>The ring around the focused card (see <see cref="FocusStyle.AddRing"/>).</summary>
    public View? Ring { get; init; }

    /// <summary>The value line of a Settings tile, which changes without a rebuild; null for other cards.</summary>
    public TextLabel? Value { get; init; }
}
