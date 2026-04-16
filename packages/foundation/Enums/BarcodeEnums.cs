namespace Sunfish.Core.Enums;

/// <summary>
/// Specifies the barcode symbology type.
/// </summary>
public enum BarcodeType
{
    /// <summary>Code 128 barcode (high density, alphanumeric).</summary>
    Code128,

    /// <summary>Code 39 barcode (alphanumeric, self-checking).</summary>
    Code39
}

/// <summary>
/// Specifies the QR code error correction level.
/// </summary>
public enum QRCodeErrorCorrection
{
    /// <summary>Low (~7% recovery).</summary>
    Low,

    /// <summary>Medium (~15% recovery).</summary>
    Medium,

    /// <summary>Quartile (~25% recovery).</summary>
    Quartile,

    /// <summary>High (~30% recovery).</summary>
    High
}
