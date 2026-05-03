namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Canonical MVP sort fields for the <c>SunfishFileManager</c> MVP surface.
/// </summary>
public enum FileManagerSortBy
{
    /// <summary>Sort by the item name (default).</summary>
    Name,

    /// <summary>Sort by file size. Directories typically sort as 0.</summary>
    Size,

    /// <summary>Sort by last-modified timestamp.</summary>
    DateModified,

    /// <summary>Sort by type (directory vs file, then extension).</summary>
    Type,
}
