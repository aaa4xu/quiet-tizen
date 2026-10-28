namespace QuietHome.Core.Platform;

/// <summary>
/// Port to the diagnostics file that tools on the computer read over SDB
/// (<c>/tmp/quiet-home-state.txt</c>; tools/screens.py, recipes, docs/09-recovery.md). The TV
/// host writes a <see cref="Diagnostics.StateReport"/> through it after every change; the
/// Tizen implementation is launcher/tizen/Platform/StateFile.cs.
/// </summary>
public interface IDiagnostics
{
    /// <summary>Replaces the file's content with <paramref name="text"/>. Failures are ignored: diagnostics must never stop the app.</summary>
    void Write(string text);
}
