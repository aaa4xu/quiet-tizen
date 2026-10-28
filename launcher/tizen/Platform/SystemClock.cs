using System;
using QuietHome.Core.Platform;

namespace QuietHome.TizenHost.Platform;

/// <summary>The TV's own clock. Implements the core's <see cref="IClock"/> port for the Quiet Home app outside demo mode.</summary>
sealed class SystemClock : IClock
{
    /// <summary>The TV's local time.</summary>
    public DateTime Now => DateTime.Now;
}
