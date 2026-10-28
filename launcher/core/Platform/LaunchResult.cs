namespace QuietHome.Core.Platform;

/// <summary>
/// What <see cref="ISystemLauncher.Launch"/> reports. <see cref="HomeController"/> shows "Could not open the
/// app." when the request could not even be created, and "Could not open this screen." when
/// sending it failed.
/// </summary>
/// <param name="Created">False when the TV could not create the request at all.</param>
/// <param name="Code">0 when the request was sent, otherwise the TV's error code.</param>
public readonly record struct LaunchResult(bool Created, int Code)
{
    /// <summary>True when the request was created and sent.</summary>
    public bool Succeeded => Created && Code == 0;
}
