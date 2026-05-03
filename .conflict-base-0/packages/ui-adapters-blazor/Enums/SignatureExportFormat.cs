namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Supported export formats for <c>SunfishSignature.ExportAsync</c>.
/// </summary>
public enum SignatureExportFormat
{
    /// <summary>Rasterized PNG as a base64 <c>data:</c> URL.</summary>
    Png,

    /// <summary>Vector SVG markup built from the recorded stroke paths.</summary>
    Svg,

    /// <summary>Structured JSON describing the raw strokes (points + style).</summary>
    Json
}
