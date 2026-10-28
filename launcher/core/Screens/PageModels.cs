using System.Collections.Generic;
using System.Linq;

namespace QuietHome.Core.Screens;

/// <summary>
/// The content of one page between the tabs and the bottom line: its cards and its own
/// texts. <see cref="ScreenBuilder"/> creates one of the three kinds for the page on screen;
/// the TV view has one page view per kind.
/// </summary>
public abstract record PageModel
{
    /// <summary>The page's focusable elements in slot order (the toolbar is not part of the page).</summary>
    public abstract IReadOnlyList<CardModel> Cards { get; }
}

/// <summary>The Home page: input cards on top and the dock of apps below.</summary>
/// <param name="Inputs">The connected inputs; when empty the page says "No connected inputs found.".</param>
/// <param name="Dock">The dock: the catalog's first apps, then "Your Apps".</param>
public sealed record HomePageModel(IReadOnlyList<InputCardModel> Inputs, IReadOnlyList<DockCardModel> Dock) : PageModel
{
    /// <summary>Text shown instead of input cards.</summary>
    public const string NoInputsText = "No connected inputs found.";

    /// <summary>The input cards, then the dock.</summary>
    public override IReadOnlyList<CardModel> Cards => Inputs.Concat<CardModel>(Dock).ToList();
}

/// <summary>The Apps page: one page of the catalog grid with its counters.</summary>
/// <param name="Apps">The cards on the open page of the grid.</param>
/// <param name="LoadFailed">True when the catalog has no apps; the page then says "Apps could not be loaded.".</param>
/// <param name="CountText">The number of apps, for example "9 apps".</param>
/// <param name="PagesText">The page number, for example "1 / 2", or null when everything fits on one page.</param>
public sealed record AppsPageModel(IReadOnlyList<AppCardModel> Apps, bool LoadFailed, string CountText, string? PagesText) : PageModel
{
    /// <summary>Text shown instead of the grid.</summary>
    public const string LoadFailedText = "Apps could not be loaded.";

    /// <summary>The app cards.</summary>
    public override IReadOnlyList<CardModel> Cards => Apps;
}

/// <summary>The Settings page: the eight tiles with their current values.</summary>
/// <param name="Tiles">The tiles in the order of <see cref="Settings.SettingsTile.All"/>.</param>
public sealed record SettingsPageModel(IReadOnlyList<SettingsTileModel> Tiles) : PageModel
{
    /// <summary>The tiles.</summary>
    public override IReadOnlyList<CardModel> Cards => Tiles;
}
