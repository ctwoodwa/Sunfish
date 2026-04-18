namespace Sunfish.Ingestion.Imagery;

/// <summary>
/// Structured EXIF-derived metadata extracted from a single image. Every field is nullable to
/// reflect the common case where EXIF is partial, missing, or unparseable; consumers should treat
/// nulls as "unknown", not zero. See Sunfish Platform spec §7.5.
/// </summary>
/// <param name="GpsLatitude">WGS-84 latitude in decimal degrees (null if unknown).</param>
/// <param name="GpsLongitude">WGS-84 longitude in decimal degrees (null if unknown).</param>
/// <param name="CapturedUtc">UTC capture timestamp (parsed from EXIF DateTimeOriginal).</param>
/// <param name="CameraMake">Camera manufacturer (EXIF IFD0 Make tag).</param>
/// <param name="CameraModel">Camera model (EXIF IFD0 Model tag).</param>
/// <param name="FocalLengthMm">Focal length in millimetres.</param>
/// <param name="WidthPx">Image width in pixels (from JPEG/PNG dimension tags).</param>
/// <param name="HeightPx">Image height in pixels (from JPEG/PNG dimension tags).</param>
public sealed record ImageryMetadata(
    double? GpsLatitude,
    double? GpsLongitude,
    DateTime? CapturedUtc,
    string? CameraMake,
    string? CameraModel,
    double? FocalLengthMm,
    int? WidthPx,
    int? HeightPx)
{
    /// <summary>An all-null metadata instance returned when no EXIF could be parsed.</summary>
    public static readonly ImageryMetadata Empty = new(null, null, null, null, null, null, null, null);
}
