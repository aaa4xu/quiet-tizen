using QuietHome.Core.Navigation;
using Xunit;

namespace QuietHome.Core.Tests.Navigation;

/// <summary>Tests of <see cref="KeyRepeatFilter"/>, which drops fast repeats of a held key.</summary>
public class KeyRepeatFilterTests
{
    /// <summary>The same key within 180 ms is dropped; after 180 ms, or a different key, it counts again.</summary>
    [Fact]
    public void DropsFastRepeatsOfTheSameKey()
    {
        var filter = new KeyRepeatFilter();
        Assert.True(filter.Accept("Right", 1000));
        Assert.False(filter.Accept("Right", 1179));
        Assert.True(filter.Accept("Right", 1180));
        Assert.True(filter.Accept("Left", 1181));
        Assert.True(filter.Accept("Right", 1182));
        Assert.Equal("Right", filter.LastKey);
    }

    /// <summary>A dropped repeat does not restart the interval.</summary>
    [Fact]
    public void DroppedKeysDoNotExtendTheWait()
    {
        var filter = new KeyRepeatFilter();
        Assert.True(filter.Accept("Down", 0));
        Assert.False(filter.Accept("Down", 100));
        Assert.True(filter.Accept("Down", 180));
    }
}
