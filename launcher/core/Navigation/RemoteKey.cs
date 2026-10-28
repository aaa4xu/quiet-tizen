namespace QuietHome.Core.Navigation;

/// <summary>
/// A remote control key as Quiet Home understands it. The TV reports keys by X11 names
/// ("Return", "XF86Back"); the TV host maps those names to these values, and
/// <see cref="Navigator"/> decides what each one does.
/// </summary>
public enum RemoteKey
{
    /// <summary>Any key Quiet Home does not use.</summary>
    Other,

    /// <summary>The Up direction key.</summary>
    Up,

    /// <summary>The Down direction key.</summary>
    Down,

    /// <summary>The Left direction key.</summary>
    Left,

    /// <summary>The Right direction key.</summary>
    Right,

    /// <summary>Select (the centre of the direction pad), or Enter on a keyboard.</summary>
    Select,

    /// <summary>Back, or Escape on a keyboard.</summary>
    Back,

    /// <summary>Exit: leaves Quiet Home like Back, but is a separate key on Samsung remotes.</summary>
    Exit,

    /// <summary>The Home key; the TV also sends it when Quiet Home is opened as the home screen.</summary>
    Home,

    /// <summary>The settings key (Smart Control's settings button, or a keyboard's menu key).</summary>
    Settings,
}
