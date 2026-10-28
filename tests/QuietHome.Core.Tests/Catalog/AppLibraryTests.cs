using System.Collections.Generic;
using System.IO;
using Xunit;

namespace QuietHome.Core.Tests.Catalog;

/// <summary>
/// Tests of <see cref="AppLibrary"/>: loading and checking the app catalogs, and focus
/// movement on the Apps page grid. They run on a computer without the TV or Samsung's assemblies.
/// </summary>
public class AppLibraryTests
{
    /// <summary>
    /// Catalogs for <see cref="CatalogLoads"/>: always the public one, and the owner's
    /// apps.local.json when it exists (it is not in git, so CI has only the public one).
    /// </summary>
    public static IEnumerable<object[]> Catalogs()
    {
        yield return new object[] { "launcher/assets/apps.json" };
        if (File.Exists(Repository.PathOf("launcher/assets/apps.local.json")))
            yield return new object[] { "launcher/assets/apps.local.json" };
    }

    /// <summary>Every catalog the package can ship passes the checks in <see cref="AppLibrary.Load"/>.</summary>
    [Theory]
    [MemberData(nameof(Catalogs))]
    public void CatalogLoads(string catalog)
    {
        var apps = AppLibrary.Load(Repository.PathOf(catalog));
        Assert.True(apps.Count >= 2, "Catalog unexpectedly small");
    }

    /// <summary>Focus movement at page edges and short rows: (catalog size, focused index, key) gives the expected index.</summary>
    [Theory]
    [InlineData(31, 0, "Up", -1)] // top row returns to tabs
    [InlineData(31, 10, "Down", 15)] // last row enters next page
    [InlineData(31, 30, "Up", 25)] // partial page returns to matching previous column
    [InlineData(31, 29, "Down", 30)] // last partial page clamps column
    [InlineData(12, 9, "Down", 11)] // partial bottom row clamps column
    [InlineData(12, 11, "Right", 11)] // last item remains selected
    [InlineData(0, 0, "Down", -1)] // empty catalog
    public void Move(int count, int absolute, string key, int expected)
    {
        Assert.Equal(expected, AppLibrary.Move(count, absolute, key));
    }

    /// <summary>For every catalog size from 1 to 47 apps (four pages), no key moves focus outside the catalog except to the tabs (-1).</summary>
    [Fact]
    public void MoveStaysInsideTheCatalogOrGoesToTheTabs()
    {
        for (int count = 1; count < 48; count++)
        {
            for (int item = 0; item < count; item++)
            {
                foreach (var key in new[] { "Left", "Right", "Up", "Down" })
                {
                    int next = AppLibrary.Move(count, item, key);
                    Assert.InRange(next, -1, count - 1);
                }
            }
        }
    }

    /// <summary>A path out of the catalog folder, a crop outside the image and a duplicate ID are each rejected.</summary>
    [Theory]
    [InlineData("[{\"Name\":\"Bad\",\"Id\":\"one\",\"Asset\":\"../escape.svg\",\"Width\":100,\"Height\":50}]")]
    [InlineData("[{\"Name\":\"Bad\",\"Id\":\"one\",\"Asset\":\"ok.svg\",\"Width\":100,\"Height\":50,\"Crop\":[0.8,0,0.4,1]}]")]
    [InlineData("[{\"Name\":\"A\",\"Id\":\"one\",\"Asset\":\"ok.svg\",\"Width\":100,\"Height\":50},{\"Name\":\"B\",\"Id\":\"one\",\"Asset\":\"ok.svg\",\"Width\":100,\"Height\":50}]")]
    public void InvalidCatalogIsRejected(string json)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            Assert.Throws<InvalidDataException>(() => AppLibrary.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
