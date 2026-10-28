using System.Collections.Generic;
using System.Text.Json;
using QuietHome.Core.Inputs;
using Xunit;

namespace QuietHome.Core.Tests.Inputs;

/// <summary>
/// Tests of reading the TV's source list (<see cref="InputList"/>) and naming the input
/// cards on the Home page (<see cref="InputCard"/>).
/// </summary>
public class InputTests
{
    const string Icons = "/usr/apps/com.samsung.tv.theme-resource/shared/res/VD_ICONS/i_icon_device_m_";

    /// <summary>Only connected inputs are kept, in the TV's order, and field names are matched without case.</summary>
    [Fact]
    public void ParseKeepsConnectedInputs()
    {
        var inputs = InputList.Parse(
            "[{\"connectionState\":true,\"sourceType\":0},"
            + "{\"ConnectionState\":false,\"SourceType\":14},"
            + "{\"CONNECTIONSTATE\":true,\"SOURCETYPE\":15,\"EditName\":\"Console\"}]");
        Assert.Equal(new[] { 0, 15 }, inputs.ConvertAll(s => s.SourceType));
        Assert.Equal("Console", inputs[1].EditName);
    }

    /// <summary>An empty array and a JSON null both mean no inputs.</summary>
    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    public void ParseEmpty(string json)
    {
        Assert.Empty(InputList.Parse(json));
    }

    /// <summary>Text that is not JSON throws, so the host can record why there are no inputs.</summary>
    [Fact]
    public void ParseBrokenJsonThrows()
    {
        Assert.ThrowsAny<JsonException>(() => InputList.Parse("[{"));
    }

    /// <summary>The fingerprint changes when an input is renamed and stays the same for an equal list.</summary>
    [Fact]
    public void FingerprintFollowsNames()
    {
        var before = new List<InputSource> { new() { SourceType = 15, EditName = "Console" } };
        var same = new List<InputSource> { new() { SourceType = 15, EditName = "Console" } };
        var renamed = new List<InputSource> { new() { SourceType = 15, EditName = "PS5" } };
        Assert.Equal(InputList.Fingerprint(before), InputList.Fingerprint(same));
        Assert.NotEqual(InputList.Fingerprint(before), InputList.Fingerprint(renamed));
    }

    /// <summary>
    /// Card names and second lines: the tuner, a URL-encoded name, the owner's rename over the
    /// detected name, an unnamed input named after its HDMI port, and one with an unknown icon.
    /// </summary>
    [Theory]
    [InlineData(0, "Ignored", null, Icons + "tv.svg", "Live TV", "Broadcast channels")]
    [InlineData(14, "Game%20console", null, Icons + "gamepad.svg", "Game console", "Connected input")]
    [InlineData(15, "Couch PC", "Detected", Icons + "hdmi2.svg", "Couch PC", "HDMI 2")]
    [InlineData(15, " ", "Apple%20TV", Icons + "hdmi2.svg", "Apple TV", "HDMI 2")]
    [InlineData(16, null, null, Icons + "hdmi3.svg", "HDMI 3", "Connected input")]
    [InlineData(16, null, "", "/usr/share/icons/unknown.png", "HDMI input", "Connected input")]
    [InlineData(16, null, null, null, "HDMI input", "Connected input")]
    public void CardText(int sourceType, string? editName, string? detectedName, string? iconPath, string name, string description)
    {
        var card = InputCard.For(new InputSource { SourceType = sourceType, EditName = editName, DetectedName = detectedName, IconPath = iconPath });
        Assert.Equal(name, card.Name);
        Assert.Equal(description, card.Description);
    }
}
