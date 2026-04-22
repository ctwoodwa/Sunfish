namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Toolbar density for <see cref="Sunfish.UIAdapters.Blazor.Components.Media.SunfishPdfViewer"/>.
/// </summary>
public enum PdfToolbarMode
{
    /// <summary>No toolbar chrome — document area fills the component.</summary>
    Hidden,

    /// <summary>Minimal toolbar — download only.</summary>
    Compact,

    /// <summary>Full toolbar — page nav, zoom, download.</summary>
    Full,
}
