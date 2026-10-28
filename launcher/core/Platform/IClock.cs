using System;

namespace QuietHome.Core.Platform;

/// <summary>
/// Port to the time shown in the top right corner of every page. The TV host passes the
/// system clock (launcher/tizen/Platform/SystemClock.cs); in demo mode the controller uses
/// <see cref="Demo.DemoClock"/>, which always says 9:41. <see cref="ClockText"/> formats it.
/// </summary>
public interface IClock
{
    /// <summary>The current local time.</summary>
    DateTime Now { get; }
}
