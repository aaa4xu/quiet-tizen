using QuietHome.Core.Platform;
using Xunit;

namespace QuietHome.Core.Tests.Platform;

/// <summary>
/// Tests of <see cref="LaunchRequest"/> and <see cref="LaunchResult"/>, which the TV host
/// passes to and gets from <see cref="ISystemLauncher"/>.
/// </summary>
public class LaunchRequestTests
{
    /// <summary>The fixed requests open Samsung's apps by their IDs, Samsung's settings optionally on one screen.</summary>
    [Fact]
    public void FixedRequests()
    {
        Assert.Equal(new LaunchRequest("org.tizen.tv-viewer"), LaunchRequest.TvViewer);
        Assert.Equal(new LaunchRequest("com.samsung.tv.csfs"), LaunchRequest.SourceEditor);
        Assert.Equal(new LaunchRequest("org.tizen.menu", "networksettings"), LaunchRequest.SamsungSettings("networksettings"));
        Assert.Null(LaunchRequest.SamsungSettings(null).MenuItem);
    }

    /// <summary>The diagnostics activity names the app, the screen when there is one, and the result code.</summary>
    [Fact]
    public void ActivityText()
    {
        Assert.Equal("launch:org.tizen.menu:networksettings:0", LaunchRequest.SamsungSettings("networksettings").Activity(0));
        Assert.Equal("launch:org.tizen.menu:0", LaunchRequest.SamsungSettings(null).Activity(0));
        Assert.Equal("launch:com.example.app:-3", new LaunchRequest("com.example.app").Activity(-3));
    }

    /// <summary>A launch succeeded only when the request was created and sent with code 0.</summary>
    [Fact]
    public void Success()
    {
        Assert.True(new LaunchResult(true, 0).Succeeded);
        Assert.False(new LaunchResult(true, -1).Succeeded);
        Assert.False(new LaunchResult(false, 0).Succeeded);
    }
}
