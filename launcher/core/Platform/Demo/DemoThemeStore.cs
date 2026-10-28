namespace QuietHome.Core.Platform.Demo;

/// <summary>
/// The theme store in demo mode (<c>quiet_demo=1</c>): it keeps the choice in memory, so
/// switching the theme during a demo does not change the owner's saved theme. The TV host
/// uses it instead of the file store while demo mode is on.
/// </summary>
public sealed class DemoThemeStore : IThemeStore
{
    string? text;

    /// <summary>Returns what was last written during this demo, or null.</summary>
    public string? Read() => text;

    /// <summary>Remembers <paramref name="text"/> until the demo ends.</summary>
    public void Write(string text) => this.text = text;
}
