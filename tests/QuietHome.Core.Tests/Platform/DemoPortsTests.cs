using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuietHome.Core.Inputs;
using QuietHome.Core.Platform;
using QuietHome.Core.Platform.Demo;
using QuietHome.Core.Settings;
using Xunit;

namespace QuietHome.Core.Tests.Platform;

/// <summary>
/// Tests of the demo mode ports in <see cref="QuietHome.Core.Platform.Demo"/>: fixed sample
/// data that README screenshots show and that changes nothing on the TV.
/// </summary>
public class DemoPortsTests
{
    /// <summary>The demo clock always shows 9:41 AM.</summary>
    [Fact]
    public void ClockShows941()
    {
        Assert.Equal("9:41 AM", ClockText.Format(new DemoClock().Now, CultureInfo.InvariantCulture));
        Assert.Equal("6:05 PM", ClockText.Format(new DateTime(2026, 10, 28, 18, 5, 0), CultureInfo.InvariantCulture));
    }

    /// <summary>The Settings page in demo mode shows the same sample values as the README screenshots: 20, 45, Off, Off.</summary>
    [Fact]
    public void SettingsStartWithSamples()
    {
        var state = new QuickSettingsState();
        state.Refresh(new DemoQuickSettings());
        string?[] values = SettingsTile.All.Take(4).Select(t => t.ValueText(state, dark: true)).ToArray();
        Assert.Equal(new[] { "20", "45", "Off", "Off" }, values);
        Assert.Equal((0d, 50d, 1d), (state.BrightnessMin, state.BrightnessMax, state.BrightnessStep));
        Assert.True(state.CanEdit("brightness"));
        Assert.Equal(QuickSettingsState.GameModeIds[0], state.GameModeId);
    }

    /// <summary>Settings changed in demo mode keep their new value and name in memory.</summary>
    [Fact]
    public void SettingsChangeInMemory()
    {
        var demo = new DemoQuickSettings();
        var state = new QuickSettingsState();
        Assert.True(demo.SetVolume(21));
        Assert.True(demo.WriteItem(QuickSettingsState.GameModeIds[0], "2"));
        Assert.True(demo.WriteItem("sleeptimer", "3"));
        state.Refresh(demo);
        Assert.Equal(21, state.Volume);
        Assert.Equal("Auto", state.Name(state.GameModeId));
        Assert.Equal("90 min", state.Name("sleeptimer"));
    }

    /// <summary>A setting the demo does not have reads as missing and cannot be written.</summary>
    [Fact]
    public void UnknownSettingIsMissing()
    {
        var demo = new DemoQuickSettings();
        Assert.Equal(new SettingReading(null, null, false), demo.ReadItem("game-mode"));
        Assert.False(demo.WriteItem("game-mode", "1"));
        Assert.Null(demo.ReadRange("sleeptimer"));
    }

    /// <summary>The demo inputs are the samples, while the input on screen and switching go to the TV.</summary>
    [Fact]
    public void InputsShowSamplesButSwitchTheTv()
    {
        var tv = new FakeInputs();
        var samples = new[] { new InputSource { ConnectionState = true, SourceType = 0 } };
        var demo = new DemoInputSources(tv, samples);
        var reading = demo.Read();
        Assert.Equal(new[] { 0 }, reading.Inputs.Select(s => s.SourceType));
        Assert.Equal("", reading.Error);
        Assert.NotSame(reading.Inputs, demo.Read().Inputs);
        Assert.Equal(7, demo.Current());
        Assert.Equal(0, demo.Switch(14));
        Assert.Equal(new[] { 14 }, tv.Switched);
        Assert.Equal(-1, demo.WatchStatus);
    }

    /// <summary>The demo theme store remembers what was written and starts empty.</summary>
    [Fact]
    public void ThemeStoreInMemory()
    {
        var store = new DemoThemeStore();
        Assert.Null(store.Read());
        store.Write("light");
        Assert.Equal("light", store.Read());
    }

    /// <summary>
    /// Stand-in for the TV's inputs in these tests: one HDMI input on screen (7), records
    /// switches, and reports that it never subscribed.
    /// </summary>
    sealed class FakeInputs : IInputSources
    {
        /// <summary>The source types <see cref="Switch"/> was called with.</summary>
        public List<int> Switched { get; } = new();

        /// <summary>Never raised in these tests.</summary>
        public event Action? Changed
        {
            add { }
            remove { }
        }

        /// <summary>Not subscribed.</summary>
        public int WatchStatus => -1;

        /// <summary>Returns a real-looking input, which demo mode must not show.</summary>
        public InputReading Read() => new(new List<InputSource> { new() { ConnectionState = true, SourceType = 7 } }, "");

        /// <summary>Returns 7.</summary>
        public int? Current() => 7;

        /// <summary>Records the switch and returns 0.</summary>
        public int Switch(int sourceType)
        {
            Switched.Add(sourceType);
            return 0;
        }
    }
}
