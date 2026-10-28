using System;
using System.Globalization;

namespace QuietHome.Core.Platform;

/// <summary>
/// The clock text in the top right corner of every page, from the time an
/// <see cref="IClock"/> gives. <see cref="HomeController"/> formats it for every page it
/// shows and every 30 seconds; the TV view only sets the label.
/// </summary>
public static class ClockText
{
    /// <summary>Returns <paramref name="time"/> as hours and minutes on a 12-hour clock in <paramref name="culture"/>, for example "9:41 AM".</summary>
    public static string Format(DateTime time, CultureInfo culture) => time.ToString("h:mm tt", culture);
}
