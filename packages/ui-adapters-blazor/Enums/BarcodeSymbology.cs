namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Supported symbologies for <see cref="Sunfish.UIAdapters.Blazor.Components.Media.SunfishBarcode"/>.
/// The MVP surface implements Code128 via a simple pass-through hash; other symbologies render
/// a placeholder message until full engines land.
/// </summary>
public enum BarcodeSymbology
{
    /// <summary>Code 128 — high-density alphanumeric (default).</summary>
    Code128,

    /// <summary>Code 39 — self-checking alphanumeric.</summary>
    Code39,

    /// <summary>EAN-13 — 13-digit retail product code.</summary>
    Ean13,

    /// <summary>EAN-8 — 8-digit compact retail product code.</summary>
    Ean8,

    /// <summary>UPC-A — 12-digit retail product code.</summary>
    UpcA,

    /// <summary>2D QR code.</summary>
    QrCode,
}
