namespace QuietHome.Core.Platform;

/// <summary>
/// Whether <see cref="HomeController"/> is showing an input preview through
/// <see cref="IInputPreview"/>. The controller keeps it and writes it to <c>preview=</c> in
/// the diagnostics file (see <see cref="PreviewStateNames.Name"/>).
/// </summary>
public enum PreviewState
{
    /// <summary>No preview: the focus is not on a previewable input card, or the app is in the background.</summary>
    Off,

    /// <summary>A preview was asked for the focused input card.</summary>
    On,

    /// <summary>The TV refused a preview; none is asked for until Quiet Home restarts.</summary>
    Unsupported,
}

/// <summary>The names of <see cref="PreviewState"/> in the diagnostics file.</summary>
public static class PreviewStateNames
{
    /// <summary>Returns "off", "on" or "unsupported", as written to <c>preview=</c>.</summary>
    public static string Name(this PreviewState state) => state switch
    {
        PreviewState.On => "on",
        PreviewState.Unsupported => "unsupported",
        _ => "off",
    };
}
