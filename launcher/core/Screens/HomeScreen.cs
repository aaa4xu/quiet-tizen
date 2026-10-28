using System.Collections.Generic;
using System.Linq;
using QuietHome.Core.Navigation;

namespace QuietHome.Core.Screens;

/// <summary>
/// Everything Quiet Home shows at one moment: the theme, the chrome around the page and the
/// page content. <see cref="HomeController"/> asks <see cref="ScreenBuilder"/> for a new one
/// after every change and passes it to <see cref="IHomeView.Render"/>.
/// </summary>
/// <param name="Dark">True for the dark theme.</param>
/// <param name="Chrome">Title, tabs, clock, toolbar and bottom lines.</param>
/// <param name="Content">The page's own cards and texts.</param>
public sealed record HomeScreen(bool Dark, ChromeModel Chrome, PageModel Content)
{
    /// <summary>Every focusable element in slot order: the page's cards, then the toolbar buttons.</summary>
    public IReadOnlyList<CardModel> Cards => Content.Cards.Concat<CardModel>(Chrome.Toolbar).ToList();

    /// <summary>The row of each element of <see cref="Cards"/>, for <see cref="Navigator"/>.</summary>
    public SlotRows Rows => new(Cards.Select(c => c.Row).ToList());
}
