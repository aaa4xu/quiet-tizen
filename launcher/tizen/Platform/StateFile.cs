using System;
using System.IO;
using QuietHome.Core.Platform;

namespace QuietHome.TizenHost.Platform;

/// <summary>
/// The diagnostics file <c>/tmp/quiet-home-state.txt</c>, which tools on the computer read
/// over SDB. Implements the core's <see cref="IDiagnostics"/> port for the Quiet Home app.
/// </summary>
sealed class StateFile : IDiagnostics
{
    /// <summary>Where the file is; tools/screens.py and the recipes read this path.</summary>
    public const string FilePath = "/tmp/quiet-home-state.txt";

    /// <summary>Replaces the file's content; errors are ignored.</summary>
    public void Write(string text)
    {
        try
        {
            File.WriteAllText(FilePath, text);
        }
        catch (Exception)
        {
            // Diagnostics must never stop the app.
        }
    }
}
