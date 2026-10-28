using System;
using System.Collections.Generic;
using System.IO;
using QuietHome.Core.Inputs;
using QuietHome.Core.Platform;

namespace QuietHome.Core.Tests.Fakes;

/// <summary>
/// A test stand-in for the TV: it implements every platform port the core's
/// <see cref="HomeController"/> relies on (inputs, quick settings, theme file, clock at
/// 6:05 PM, app launcher, diagnostics file, input preview) in memory. Tests set what the TV reports and
/// what fails, then check what the controller asked of it. The Tizen implementations it
/// replaces live in launcher/tizen/Platform. Used through <see cref="HomeHarness"/>.
/// </summary>
sealed class FakeTv : IInputSources, IQuickSettings, IThemeStore, IClock, ISystemLauncher, IDiagnostics, IInputPreview
{
    /// <summary>Creates the TV with a brightness of 30 and the sleep timer off.</summary>
    public FakeTv()
    {
        Items["brightness"] = "30";
        Items["sleeptimer"] = "0";
    }

    /// <summary>The ports bundle over this TV.</summary>
    public HomePorts Ports => new(this, this, this, this, this, this, this);

    /// <summary>The connected inputs.</summary>
    public List<InputSource> Inputs { get; set; } = new();

    /// <summary>The input on screen.</summary>
    public int CurrentSource { get; set; }

    /// <summary>How many times the input list was read.</summary>
    public int Reads { get; private set; }

    /// <summary>The inputs switched to, in order.</summary>
    public List<int> Switched { get; } = new();

    /// <summary>What <see cref="Switch"/> returns; negative means the switch failed.</summary>
    public int SwitchResult { get; set; }

    /// <summary>The settings menu items by ID.</summary>
    public Dictionary<string, string> Items { get; } = new();

    /// <summary>Items the TV reports but does not let the owner change for the current input.</summary>
    public HashSet<string> Locked { get; } = new();

    /// <summary>The writes to menu items, in order.</summary>
    public List<(string Id, string Value)> Written { get; } = new();

    /// <summary>When true, the TV refuses every item write.</summary>
    public bool RefuseWrites { get; set; }

    /// <summary>When set, the TV accepts writes to this item but keeps its old value.</summary>
    public string? IgnoreWritesTo { get; set; }

    /// <summary>The master volume, or null when the TV does not report it.</summary>
    public int? Volume { get; set; } = 15;

    /// <summary>The volumes set, in order.</summary>
    public List<int> VolumesSet { get; } = new();

    /// <summary>The stored theme text, or null.</summary>
    public string? Theme { get; set; }

    /// <summary>When true, saving the theme fails with "disk full".</summary>
    public bool ThemeFails { get; set; }

    /// <summary>The launch requests sent, in order.</summary>
    public List<LaunchRequest> Launched { get; } = new();

    /// <summary>What <see cref="Launch"/> returns.</summary>
    public LaunchResult LaunchResult { get; set; } = new(true, 0);

    /// <summary>The input whose preview is asked for, or null while none is.</summary>
    public int? Previewing { get; private set; }

    /// <summary>The slot of the card <see cref="Previewing"/> is shown in, or -1.</summary>
    public int PreviewSlot { get; private set; } = -1;

    /// <summary>When true, the TV refuses every preview and raises <see cref="Failed"/>.</summary>
    public bool PreviewFails { get; set; }

    /// <summary>Previews, hides, releases, mutes, input switches and launches in the order they reached the TV, for example "silence", "preview 14", "release", "switch 14", "launch".</summary>
    public List<string> Events { get; } = new();

    /// <summary>The last diagnostics text.</summary>
    public string State { get; private set; } = "";

    /// <summary>Raised by <see cref="RaiseChanged"/>.</summary>
    public event Action? Changed;

    /// <summary>Raised by <see cref="Show"/> when <see cref="PreviewFails"/> is set.</summary>
    public event Action? Failed;

    /// <summary>Subscribed.</summary>
    public int WatchStatus => 0;

    /// <summary>6:05 PM.</summary>
    public DateTime Now => new(2026, 10, 28, 18, 5, 0);

    /// <summary>Simulates the TV's change notice.</summary>
    public void RaiseChanged() => Changed?.Invoke();

    /// <summary>Counts the read and returns a copy of the inputs.</summary>
    public InputReading Read()
    {
        Reads++;
        return new InputReading(new List<InputSource>(Inputs), "");
    }

    /// <summary>Returns the input on screen.</summary>
    public int? Current() => CurrentSource;

    /// <summary>Records the switch and returns <see cref="SwitchResult"/>; a successful switch puts that input on screen.</summary>
    public int Switch(int sourceType)
    {
        Switched.Add(sourceType);
        Events.Add("switch " + sourceType);
        if (SwitchResult >= 0)
            CurrentSource = sourceType;
        return SwitchResult;
    }

    /// <summary>Returns the volume, or null when it is not reported.</summary>
    public int? GetVolume() => Volume;

    /// <summary>Records and sets the volume; fails when the volume is not reported.</summary>
    public bool SetVolume(int volume)
    {
        if (Volume == null)
            return false;
        VolumesSet.Add(volume);
        Volume = volume;
        return true;
    }

    /// <summary>Returns the item with its value as name, changeable unless locked; a missing item has no value.</summary>
    public SettingReading ReadItem(string id) =>
        Items.TryGetValue(id, out var value) ? new SettingReading(value, value, !Locked.Contains(id)) : new SettingReading(null, null, false);

    /// <summary>Returns 0..50 by 1.</summary>
    public SettingRange? ReadRange(string id) => new SettingRange(0, 50, 1);

    /// <summary>Records the write and applies it, unless writes are refused or ignored for that item.</summary>
    public bool WriteItem(string id, string value)
    {
        Written.Add((id, value));
        if (RefuseWrites)
            return false;
        if (id != IgnoreWritesTo)
            Items[id] = value;
        return true;
    }

    /// <summary>Returns the stored theme.</summary>
    string? IThemeStore.Read() => Theme;

    /// <summary>Stores the theme, or fails when <see cref="ThemeFails"/> is set.</summary>
    public void Write(string text)
    {
        if (ThemeFails)
            throw new IOException("disk full");
        Theme = text;
    }

    /// <summary>Records the request and returns <see cref="LaunchResult"/>.</summary>
    public LaunchResult Launch(LaunchRequest request)
    {
        Launched.Add(request);
        Events.Add("launch");
        return LaunchResult;
    }

    /// <summary>Records the preview, or refuses it and raises <see cref="Failed"/> when <see cref="PreviewFails"/> is set.</summary>
    public void Show(int sourceType, int slot)
    {
        Events.Add("preview " + sourceType);
        if (PreviewFails)
        {
            Failed?.Invoke();
            return;
        }
        Previewing = sourceType;
        PreviewSlot = slot;
    }

    /// <summary>Records that the preview was put away.</summary>
    public void Hide()
    {
        Events.Add("hide");
        Previewing = null;
        PreviewSlot = -1;
    }

    /// <summary>Records that the input's sound was muted under the home screen.</summary>
    public void Silence() => Events.Add("silence");

    /// <summary>Records that Quiet Home gave the screen up, ending any preview, as "release" or "release+sound".</summary>
    public void Release(bool sound)
    {
        Events.Add(sound ? "release+sound" : "release");
        Previewing = null;
        PreviewSlot = -1;
    }

    /// <summary>Keeps the diagnostics text.</summary>
    void IDiagnostics.Write(string text) => State = text;
}
