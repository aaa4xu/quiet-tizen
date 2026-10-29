using System;
using System.Collections.Generic;

namespace QuietHome.Core.Platform.Demo;

/// <summary>
/// The CEC port in demo mode: no input has a CEC device, so the demo inputs show only what
/// their sample <see cref="Inputs.InputSource.PowerState"/> says. <see cref="HomeController"/>
/// puts it in front of the TV's own CEC port while demo mode is on.
/// </summary>
public sealed class DemoCecDevices : ICecDevices
{
    /// <summary>Never raised: the demo has no CEC devices.</summary>
    public event Action? Changed
    {
        add { }
        remove { }
    }

    /// <summary>Returns an empty set.</summary>
    public IReadOnlySet<int> Inputs() => new HashSet<int>();
}
