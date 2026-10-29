using System;
using System.Collections.Generic;

namespace QuietHome.Core.Platform;

/// <summary>
/// Port to the TV's HDMI-CEC service: which inputs have a device that answers CEC, and a
/// notice when the TV's CEC device list changes. <see cref="HomeController"/> uses it only to
/// tell a device in standby from one that is off (see <see cref="Inputs.InputPowers"/>). The
/// TV host implements it over D-Bus (launcher/tizen/Platform/TizenCecDevices.cs); demo mode
/// uses <see cref="Demo.DemoCecDevices"/>. See docs/13-input-state.md.
/// </summary>
public interface ICecDevices
{
    /// <summary>
    /// Raised on the UI thread when the TV reports that its CEC device list changed; it also
    /// came a few seconds after a console turned on or went to rest mode. The handler calls
    /// <see cref="Inputs"/> again.
    /// </summary>
    event Action? Changed;

    /// <summary>
    /// Returns the source types of the inputs whose HDMI port has a CEC device, including one
    /// in standby. Never throws: a TV without the service gives an empty set.
    /// </summary>
    IReadOnlySet<int> Inputs();
}
