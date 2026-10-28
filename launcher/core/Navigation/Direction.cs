namespace QuietHome.Core.Navigation;

/// <summary>
/// A direction key on the remote. <see cref="Catalog.CatalogGrid.Move"/> uses it to move
/// focus between app cards; <see cref="RemoteKey"/> has the same four keys among the others.
/// </summary>
public enum Direction
{
    /// <summary>The Left key.</summary>
    Left,

    /// <summary>The Right key.</summary>
    Right,

    /// <summary>The Up key.</summary>
    Up,

    /// <summary>The Down key.</summary>
    Down,
}
