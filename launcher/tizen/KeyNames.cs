using QuietHome.Core.Navigation;

namespace QuietHome.TizenHost;

/// <summary>
/// The TV's X11 key names for the keys Quiet Home uses. The Quiet Home app translates every
/// key press with it before passing it to the core's HomeController.
/// </summary>
static class KeyNames
{
    /// <summary>Returns the remote key that <paramref name="name"/> stands for, or <see cref="RemoteKey.Other"/>.</summary>
    public static RemoteKey ToRemoteKey(string name) => name switch
    {
        "Up" => RemoteKey.Up,
        "Down" => RemoteKey.Down,
        "Left" => RemoteKey.Left,
        "Right" => RemoteKey.Right,
        "Return" or "KP_Enter" => RemoteKey.Select,
        "XF86Back" or "Back" or "Escape" => RemoteKey.Back,
        "XF86Exit" => RemoteKey.Exit,
        "XF86Home" => RemoteKey.Home,
        "XF86SysMenu" or "XF86More" => RemoteKey.Settings,
        _ => RemoteKey.Other,
    };
}
