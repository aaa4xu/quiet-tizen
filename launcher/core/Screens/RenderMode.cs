namespace QuietHome.Core.Screens;

/// <summary>How <see cref="IHomeView.Render"/> should show a new <see cref="HomeScreen"/>.</summary>
public enum RenderMode
{
    /// <summary>Draw the page from scratch: another page, other inputs, another Apps page, theme or demo mode.</summary>
    Rebuild,

    /// <summary>Update focus and values at once, without animation (after resuming).</summary>
    Refresh,

    /// <summary>Move the focus with a short animation and update values (after a key or a setting change).</summary>
    AnimateFocus,
}
