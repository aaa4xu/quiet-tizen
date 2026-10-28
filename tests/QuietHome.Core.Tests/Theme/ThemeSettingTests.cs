using QuietHome.Core.Theme;
using Xunit;

namespace QuietHome.Core.Tests.Theme;

/// <summary>Tests of <see cref="ThemeSetting"/>, the stored form of the theme choice.</summary>
public class ThemeSettingTests
{
    /// <summary>Only "light" (white space ignored) gives the light theme; anything else, or nothing stored, is dark.</summary>
    [Theory]
    [InlineData("light", false)]
    [InlineData(" light\n", false)]
    [InlineData("dark", true)]
    [InlineData("Light", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public void StoredTextGivesTheme(string? stored, bool dark)
    {
        Assert.Equal(dark, ThemeSetting.IsDark(stored));
    }

    /// <summary>The stored text reads back as the same theme.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TextRoundTrips(bool dark)
    {
        Assert.Equal(dark, ThemeSetting.IsDark(ThemeSetting.Text(dark)));
    }
}
