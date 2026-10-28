using QuietHome.Core.Platform;

namespace QuietHome.Core;

/// <summary>
/// The TV behind <see cref="HomeController"/>: one implementation of each platform port. The
/// TV host passes the Tizen implementations (launcher/tizen/Platform); tests pass stand-ins.
/// Demo mode does not replace these: the controller puts its own demo ports in front of them.
/// </summary>
/// <param name="Inputs">The TV's inputs.</param>
/// <param name="QuickSettings">Volume and settings menu items.</param>
/// <param name="ThemeStore">Where the theme choice is kept.</param>
/// <param name="Clock">The TV's clock.</param>
/// <param name="Launcher">Opens other apps.</param>
/// <param name="Diagnostics">The diagnostics file.</param>
/// <param name="Preview">The live picture of an input inside its card.</param>
public sealed record HomePorts(
    IInputSources Inputs,
    IQuickSettings QuickSettings,
    IThemeStore ThemeStore,
    IClock Clock,
    ISystemLauncher Launcher,
    IDiagnostics Diagnostics,
    IInputPreview Preview);
