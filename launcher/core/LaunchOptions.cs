using System;
using QuietHome.Core.Navigation;

namespace QuietHome.Core;

/// <summary>
/// Options a launch request can carry as app-control extras. The TV host reads them from
/// every request it receives, including requests to the already running app, and
/// <see cref="HomeController.AppControl"/> applies them:
/// <c>quiet_demo=1</c> turns demo mode on (any launch without it turns it off),
/// <c>quiet_theme=light|dark</c> overrides the theme until demo mode ends, and
/// <c>quiet_page=apps|settings</c> opens that page. The README screenshot recipe uses all three.
/// </summary>
public sealed class LaunchOptions
{
    /// <summary>True when the request asks for demo mode (public catalog, sample inputs and values).</summary>
    public bool Demo { get; }

    /// <summary>The requested theme: true for dark, false for light, null when the request names none or an unknown one.</summary>
    public bool? Dark { get; }

    /// <summary>The page to open; <see cref="Navigation.Page.Home"/> unless the request names Apps or Settings.</summary>
    public Page Page { get; }

    /// <summary>Creates options; use <see cref="Parse"/> to read them from a request.</summary>
    LaunchOptions(bool demo, bool? dark, Page page)
    {
        Demo = demo;
        Dark = dark;
        Page = page;
    }

    /// <summary>
    /// Reads the options from a launch request. <paramref name="extra"/> returns the value
    /// of an extra by its key, or null when the request does not have it.
    /// </summary>
    public static LaunchOptions Parse(Func<string, string?> extra)
    {
        string? theme = extra("quiet_theme");
        Page page = extra("quiet_page") switch
        {
            "settings" => Page.Settings,
            "apps" => Page.Apps,
            _ => Page.Home,
        };
        return new LaunchOptions(extra("quiet_demo") == "1", theme == "dark" ? true : theme == "light" ? false : null, page);
    }
}
