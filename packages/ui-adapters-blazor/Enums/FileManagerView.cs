namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Canonical MVP view modes for the <c>Components/Forms/Inputs/SunfishFileManager</c>
/// MVP surface. Distinct from the existing
/// <see cref="Sunfish.Foundation.Models.FileManagerViewType"/>, which is used
/// by the rich <c>SunfishFileManager&lt;TItem&gt;</c> component and currently
/// supports only <c>Grid</c> and <c>ListView</c>.
/// </summary>
public enum FileManagerView
{
    /// <summary>Grid / thumbnail view.</summary>
    Grid,

    /// <summary>Simple list view (names only).</summary>
    List,

    /// <summary>Detailed columns view (name, size, modified, type).</summary>
    Details,
}
