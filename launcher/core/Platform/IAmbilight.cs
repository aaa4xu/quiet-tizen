namespace QuietHome.Core.Platform;

/// <summary>
/// The ambilight sender on the TV: whether its program is installed, whether it is running
/// now, and starting or stopping it. The TV host implements it in
/// <c>launcher/tizen/Platform/TizenAmbilight</c> by running the packaged
/// <c>AmbilightLoop.dll</c>; tests and demo mode use in-memory stand-ins. The core's
/// <see cref="HomeController"/> toggles it from the Home toolbar.
/// </summary>
public interface IAmbilight
{
    /// <summary>True when the sender's program is installed in the app package.</summary>
    bool Available { get; }

    /// <summary>True when the sender process is running now.</summary>
    bool IsOn { get; }

    /// <summary>Starts the sender if it is not running; returns false when it could not start.</summary>
    bool Start();

    /// <summary>Stops the sender; returns false when it could not stop.</summary>
    bool Stop();

    /// <summary>
    /// Starts the sender again when the owner left it on before Quiet Home restarted. Does
    /// nothing when the saved preference is off or the program is missing.
    /// </summary>
    void Restore();
}
