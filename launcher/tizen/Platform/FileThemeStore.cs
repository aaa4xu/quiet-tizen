using System;
using System.IO;
using QuietHome.Core.Platform;

namespace QuietHome.TizenHost.Platform;

/// <summary>
/// Keeps the owner's theme choice in a file named "theme" in the app container's data
/// directory, which survives restarts and updates of Quiet Home. Implements the core's
/// <see cref="IThemeStore"/> port for the Quiet Home app outside demo mode.
/// </summary>
sealed class FileThemeStore : IThemeStore
{
    readonly string path;

    /// <summary>Creates the store for the container's data directory <paramref name="dataDirectory"/>.</summary>
    public FileThemeStore(string dataDirectory) => path = Path.Combine(dataDirectory, "theme");

    /// <summary>Returns the file's text, or null when it is missing or unreadable.</summary>
    public string? Read()
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Replaces the file's text; IO errors pass to the caller.</summary>
    public void Write(string text) => File.WriteAllText(path, text);
}
