namespace QuietHome.Core.Navigation;

/// <summary>
/// Which setting the remote is adjusting on the Settings page. While it is not
/// <see cref="None"/>, Left and Right change that setting instead of moving focus
/// (see <see cref="Navigator"/>), and Select, Back or Exit finish the adjustment.
/// </summary>
public enum Adjustment
{
    /// <summary>Normal navigation.</summary>
    None,

    /// <summary>Left / Right change the master volume.</summary>
    Volume,

    /// <summary>Left / Right change the picture brightness.</summary>
    Brightness,
}

/// <summary>Text forms of <see cref="Adjustment"/> for the screen and the diagnostics file.</summary>
public static class AdjustmentNames
{
    /// <summary>Returns "volume", "brightness" or "" (no adjustment), as written to <c>editing=</c> in the diagnostics file and shown in the hint.</summary>
    public static string Name(this Adjustment adjustment) => adjustment switch
    {
        Adjustment.Volume => "volume",
        Adjustment.Brightness => "brightness",
        _ => "",
    };
}
