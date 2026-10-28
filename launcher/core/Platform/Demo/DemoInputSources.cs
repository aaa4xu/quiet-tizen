using System;
using System.Collections.Generic;
using QuietHome.Core.Inputs;

namespace QuietHome.Core.Platform.Demo;

/// <summary>
/// The inputs in demo mode (<c>quiet_demo=1</c>): the Home page shows fixed sample inputs,
/// so README screenshots do not show the owner's devices. Everything else (the input on
/// screen, switching, change notices) still goes to the TV, so Back returns to the real
/// picture. <see cref="HomeController"/> puts it in front of the TV's inputs while demo
/// mode is on; the TV host passes the samples in <see cref="HomeConfig"/>, because their
/// icon paths depend on the TV.
/// </summary>
public sealed class DemoInputSources : IInputSources
{
    readonly IInputSources tv;
    readonly IReadOnlyList<InputSource> samples;

    /// <summary>Creates the demo inputs over the TV's real ones; <paramref name="samples"/> are shown instead of the connected inputs.</summary>
    public DemoInputSources(IInputSources tv, IReadOnlyList<InputSource> samples)
    {
        this.tv = tv;
        this.samples = samples;
    }

    /// <summary>Never raised: the sample list does not change.</summary>
    public event Action? Changed
    {
        add { }
        remove { }
    }

    /// <summary>The TV's own subscription status, so the diagnostics file reads the same in demo mode.</summary>
    public int WatchStatus => tv.WatchStatus;

    /// <summary>Returns a new list of the samples with no error.</summary>
    public InputReading Read() => new(new List<InputSource>(samples), "");

    /// <summary>Returns the input the TV really shows.</summary>
    public int? Current() => tv.Current();

    /// <summary>Switches the TV's real input.</summary>
    public int Switch(int sourceType) => tv.Switch(sourceType);
}
