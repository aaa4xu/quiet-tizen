using System;
using System.IO;

namespace QuietHome.Core.Tests;

/// <summary>
/// Test helper that finds files of this checkout, such as the app catalogs. Tests run from
/// <c>tests/QuietHome.Core.Tests/bin/...</c>, so relative paths would not work; every path
/// here starts at the repository root.
/// </summary>
static class Repository
{
    /// <summary>Absolute path of the repository root.</summary>
    public static string Root { get; } = FindRoot();

    /// <summary>Turns a path relative to the repository root, like "launcher/assets/apps.json", into an absolute one.</summary>
    public static string PathOf(string relative) => Path.Combine(Root, relative);

    /// <summary>Walks up from the test binaries to the first folder that holds global.json, which is the repository root.</summary>
    static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")))
                return directory.FullName;
        }
        throw new InvalidOperationException("No global.json above " + AppContext.BaseDirectory);
    }
}
