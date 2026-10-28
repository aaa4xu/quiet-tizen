using System;

namespace QuietHome.Core.Platform.Demo;

/// <summary>
/// The clock in demo mode (<c>quiet_demo=1</c>): always 9:41 in the morning, so README
/// screenshots do not depend on when they were taken. The TV host uses it instead of the
/// system clock while demo mode is on.
/// </summary>
public sealed class DemoClock : IClock
{
    /// <summary>9:41 AM on a fixed day; only the time is shown.</summary>
    public DateTime Now { get; } = new(2007, 1, 9, 9, 41, 0);
}
