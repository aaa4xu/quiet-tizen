using System.Collections.Generic;
using System.IO;
using QuietHome.Core.Catalog;
using Xunit;

namespace QuietHome.Core.Tests.Catalog;

/// <summary>
/// Tests of <see cref="AppCatalog"/>: loading and checking the app catalogs, and choosing
/// the dock apps. They run on a computer without the TV or Samsung's assemblies.
/// </summary>
public class AppCatalogTests
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

    /// <summary>Every catalog the package can ship passes the checks in <see cref="AppCatalog.Load"/>.</summary>
    [Theory]
    [MemberData(nameof(Catalogs))]
    public void CatalogLoads(string catalog)
    {
        var apps = AppCatalog.Load(Repository.PathOf(catalog));
        Assert.True(apps.Count >= 2, "Catalog unexpectedly small");
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
            Assert.Throws<InvalidDataException>(() => AppCatalog.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>The dock takes the first five apps and skips utility cards wherever they are.</summary>
    [Fact]
    public void DockSkipsUtilityCards()
    {
        var apps = new List<LibraryApp>();
        for (int i = 0; i < 8; i++)
            apps.Add(new LibraryApp { Id = "app" + i, Utility = i == 1 });
        Assert.Equal(new[] { "app0", "app2", "app3", "app4", "app5" }, AppCatalog.DockApps(apps).ConvertAll(a => a.Id));
    }

    /// <summary>A catalog with fewer than five apps puts all of them in the dock.</summary>
    [Fact]
    public void ShortCatalogFillsPartOfTheDock()
    {
        var apps = new List<LibraryApp> { new() { Id = "one" }, new() { Id = "more", Utility = true } };
        Assert.Equal(new[] { "one" }, AppCatalog.DockApps(apps).ConvertAll(a => a.Id));
    }

}
