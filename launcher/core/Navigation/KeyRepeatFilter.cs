namespace QuietHome.Core.Navigation;

/// <summary>
/// Drops a key that arrives again too soon after the same key. Holding a direction key on
/// the remote sends a fast stream of repeats; without this filter focus would race across
/// the page. The TV host asks <see cref="Accept"/> for every key press before handling it.
/// The time is passed in, so tests need no clock.
/// </summary>
public sealed class KeyRepeatFilter
{
    /// <summary>Shortest time between two presses of the same key that both count.</summary>
    public const long MinimumIntervalMs = 180;

    long lastAcceptedAt;

    /// <summary>Name of the last accepted key, as the TV reported it; "" before the first key. Written to the diagnostics file.</summary>
    public string LastKey { get; private set; } = "";

    /// <summary>
    /// Returns false when <paramref name="key"/> is the same key as the last accepted one
    /// and less than <see cref="MinimumIntervalMs"/> has passed since then; otherwise
    /// remembers this press and returns true. <paramref name="nowMs"/> is a monotonic clock
    /// in milliseconds.
    /// </summary>
    public bool Accept(string key, long nowMs)
    {
        if (key == LastKey && nowMs - lastAcceptedAt < MinimumIntervalMs)
            return false;
        LastKey = key;
        lastAcceptedAt = nowMs;
        return true;
    }
}
