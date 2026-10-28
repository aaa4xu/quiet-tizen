namespace QuietHome.Core.Platform;

/// <summary>
/// Port that opens other apps on the TV: the owner's apps, Samsung's settings, Samsung's
/// source list and the TV picture after an input switch. The TV host implements it with
/// Tizen's app control API (launcher/tizen/Platform/TizenSystemLauncher.cs), which also adds
/// the extras Samsung's own apps expect. The host builds a <see cref="LaunchRequest"/> for
/// each Select on a card and reports the result.
/// </summary>
public interface ISystemLauncher
{
    /// <summary>Sends <paramref name="request"/> to the TV and returns whether it went out.</summary>
    LaunchResult Launch(LaunchRequest request);
}
