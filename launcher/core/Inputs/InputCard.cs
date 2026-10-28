using System;
using System.Text.RegularExpressions;

namespace QuietHome.Core.Inputs;

/// <summary>
/// The text of one input card on the Home page: the big name under the card and the
/// smaller line below it. <see cref="Screens.ScreenBuilder"/> gets one per connected
/// <see cref="InputSource"/>; the TV view draws the TV's own icon on the card.
/// </summary>
public sealed class InputCard
{
    /// <summary>Card title, for example "Live TV", "Game console" or "HDMI 3".</summary>
    public string Name { get; }

    /// <summary>Line under the title: "Broadcast channels", the HDMI port, or "Connected input".</summary>
    public string Description { get; }

    /// <summary>Creates a card; use <see cref="For"/> to get one for an input.</summary>
    InputCard(string name, string description)
    {
        Name = name;
        Description = description;
    }

    /// <summary>
    /// Names an input the way the owner expects. The tuner is always "Live TV". Otherwise
    /// the owner's rename in Samsung's source list wins over the name the device reports;
    /// both arrive URL-encoded. An input without either name is called after its HDMI port,
    /// taken from the icon file name ("...hdmi3.svg" gives "HDMI 3"), or "HDMI input" when
    /// the icon shows no port. A named HDMI input shows its port on the second line.
    /// </summary>
    public static InputCard For(InputSource source)
    {
        string? raw = !string.IsNullOrWhiteSpace(source.EditName) ? source.EditName : source.DetectedName;
        var port = Regex.Match(source.IconPath ?? "", @"hdmi(\d+)\.svg$");
        string hdmi = port.Success ? "HDMI " + port.Groups[1].Value : "";
        string name = source.SourceType == 0 ? "Live TV" : !string.IsNullOrWhiteSpace(raw) ? Uri.UnescapeDataString(raw) : hdmi != "" ? hdmi : "HDMI input";
        string description = source.SourceType == 0 ? "Broadcast channels" : name != hdmi && hdmi != "" ? hdmi : "Connected input";
        return new InputCard(name, description);
    }
}
