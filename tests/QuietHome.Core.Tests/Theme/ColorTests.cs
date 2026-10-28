using QuietHome.Core.Theme;
using Xunit;

namespace QuietHome.Core.Tests.Theme;

/// <summary>
/// Tests of the theme colors: reading catalog colors (<see cref="Rgba.FromHex"/>), the two
/// palettes and the placeholder card math in <see cref="ColorMath"/>. The expected numbers
/// were computed with the color code of the former launcher/tizen/Home.cs before it moved to the core,
/// so a passing test means the cards look exactly as before.
/// </summary>
public class ColorTests
{
    /// <summary>"#RRGGBB" gives each channel as a fraction of 255, with full opacity.</summary>
    [Fact]
    public void FromHexReadsEachChannel()
    {
        Assert.Equal(new Rgba(0.101960786f, 0.16862746f, 0.23529412f, 1), Rgba.FromHex("#1A2B3C"));
    }

    /// <summary>A color that cannot be read falls back to white instead of failing.</summary>
    [Theory]
    [InlineData("nope")]
    [InlineData("")]
    [InlineData(null)]
    public void FromHexFallsBackToWhite(string? hex)
    {
        Assert.Equal(Rgba.White, Rgba.FromHex(hex));
    }

    /// <summary>The palettes keep the exact colors the launcher used before the refactoring.</summary>
    [Fact]
    public void PalettesKeepTheirColors()
    {
        Assert.Equal(new Rgba(0.95f, 0.96f, 0.98f, 1), ThemeColors.Dark.Ink);
        Assert.Equal(new Rgba(0.05f, 0.06f, 0.08f, 1), ThemeColors.Dark.Window);
        Assert.Equal(new Rgba(.11f, .13f, .17f, 1), ThemeColors.Dark.InputCard);
        Assert.Equal(new Rgba(0.114f, 0.114f, 0.122f, 1), ThemeColors.Light.Ink);
        Assert.Equal(new Rgba(1, 1, 1, .7f), ThemeColors.Light.TabFocus);
        Assert.Equal(new Rgba(1, 1, 1, .92f), ThemeColors.Light.InputCard);
        Assert.Same(ThemeColors.Dark, ThemeColors.For(true));
        Assert.Same(ThemeColors.Light, ThemeColors.For(false));
    }

    /// <summary>
    /// For every card color in the public catalog: whether its text is dark, and the exact
    /// tone of the circle drawn on a placeholder card (darker under dark text, lighter under white).
    /// </summary>
    [Theory]
    [InlineData("#000000", false, 0.044678807f, 0.044678807f, 0.044678807f)]
    [InlineData("#023D4A", false, 0.088869765f, 0.29676476f, 0.349127f)]
    [InlineData("#0779FF", false, 0.14400925f, 0.5414709f, 1f)]
    [InlineData("#111318", false, 0.12392497f, 0.13245808f, 0.15360233f)]
    [InlineData("#1ED760", true, 0f, 0.7757361f, 0.3105665f)]
    [InlineData("#292929", false, 0.21591376f, 0.21591374f, 0.21591373f)]
    [InlineData("#6C21C9", false, 0.48015377f, 0.21668205f, 0.8595344f)]
    [InlineData("#9146FF", false, 0.6282624f, 0.34950563f, 1f)]
    [InlineData("#EEF0F8", true, 0.86670333f, 0.87443143f, 0.9053672f)]
    [InlineData("#FFFFFF", true, 0.9325084f, 0.93250835f, 0.93250823f)]
    public void PlaceholderTextAndTone(string hex, bool darkText, float r, float g, float b)
    {
        var background = Rgba.FromHex(hex);
        Assert.Equal(darkText, ColorMath.DarkText(background));
        Assert.Equal(new Rgba(r, g, b, 1), ColorMath.Tone(background, darkText ? -.06 : .06));
    }
}
