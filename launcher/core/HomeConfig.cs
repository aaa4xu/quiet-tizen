using System.Collections.Generic;
using QuietHome.Core.Inputs;

namespace QuietHome.Core;

/// <summary>
/// The device-specific data <see cref="HomeController"/> needs, which the TV host knows: where
/// the catalogs are and which sample inputs demo mode shows (their icons are the TV's own files).
/// </summary>
/// <param name="CatalogPath">The owner's catalog, <c>apps.json</c>.</param>
/// <param name="PublicCatalogPath">The public catalog demo mode shows, <c>apps.public.json</c>.</param>
/// <param name="DemoInputs">The inputs demo mode shows.</param>
public sealed record HomeConfig(string CatalogPath, string PublicCatalogPath, IReadOnlyList<InputSource> DemoInputs);
